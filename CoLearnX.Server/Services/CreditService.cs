using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Payments;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ICreditService
{
    Task<IReadOnlyList<CreditPackageDto>> GetPackagesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CreditLedgerItemDto>> GetMyLedgerAsync(int userId, CancellationToken ct = default);
    Task<CreditLedgerItemDto> TopUpAsync(int userId, TopUpRequest request, CancellationToken ct = default);
    Task<CreatePayPalOrderResponse> CreatePayPalOrderAsync(int userId, CreatePayPalOrderRequest request, CancellationToken ct = default);
    Task<CreditLedgerItemDto> CapturePayPalOrderAsync(int userId, CapturePayPalOrderRequest request, CancellationToken ct = default);
}

public class CreditService(CoLearnXDbContext db, IPayPalClient payPal, Microsoft.Extensions.Options.IOptions<PayPalOptions> payPalOptions) : ICreditService
{
    public async Task<IReadOnlyList<CreditPackageDto>> GetPackagesAsync(CancellationToken ct = default)
    {
        return await db.CreditPackages.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new CreditPackageDto(
                p.Id,
                p.PayAudCents,
                p.PayAudCents / 100m,
                p.Credits,
                p.Note,
                p.IsBestValue))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CreditLedgerItemDto>> GetMyLedgerAsync(int userId, CancellationToken ct = default)
    {
        return await db.CreditTransactions.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new CreditLedgerItemDto(t.Id, t.CreatedAt, t.Type.ToString(), t.Description,
                t.Delta, t.BalanceAfter, t.HeldAfter, t.RelatedEnrollmentId))
            .ToListAsync(ct);
    }

    public async Task<CreditLedgerItemDto> TopUpAsync(int userId, TopUpRequest request, CancellationToken ct = default)
    {
        // Dev-only simulated top-up kept for offline demos. Prefer PayPal create/capture in UI.
        var package = await db.CreditPackages.FirstOrDefaultAsync(p => p.Id == request.CreditPackageId && p.IsActive, ct)
            ?? throw new InvalidOperationException("Invalid credit package.");

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var user = await db.Users.FirstAsync(u => u.Id == userId, ct);

            var payment = new PaymentTransaction
            {
                UserId = userId,
                CreditPackageId = package.Id,
                Provider = "PayPal-Sim",
                ProviderReference = request.ProviderReference ?? $"SIM-{Guid.NewGuid():N}",
                AmountAudCents = package.PayAudCents,
                CreditsGranted = package.Credits,
                Status = PaymentStatus.Completed,
            };
            db.PaymentTransactions.Add(payment);
            await db.SaveChangesAsync(ct);

            user.CreditBalance += package.Credits;
            var ledger = new CreditTransaction
            {
                UserId = userId,
                Type = CreditTransactionType.TopUp,
                Description = $"Simulated PayPal package ${package.PayAudCents / 100m:0} → +{package.Credits}",
                Delta = package.Credits,
                BalanceAfter = user.CreditBalance,
                HeldAfter = user.HeldCredits,
                RelatedPaymentId = payment.Id,
            };
            db.CreditTransactions.Add(ledger);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new CreditLedgerItemDto(ledger.Id, ledger.CreatedAt, ledger.Type.ToString(), ledger.Description, ledger.Delta, ledger.BalanceAfter, ledger.HeldAfter);
        });
    }

    public async Task<CreatePayPalOrderResponse> CreatePayPalOrderAsync(int userId, CreatePayPalOrderRequest request, CancellationToken ct = default)
    {
        var options = payPalOptions.Value;
        var package = await db.CreditPackages.FirstOrDefaultAsync(p => p.Id == request.CreditPackageId && p.IsActive, ct)
            ?? throw new InvalidOperationException("Invalid credit package.");

        var amount = package.PayAudCents / 100m;
        var customId = $"u{userId}:p{package.Id}";
        var description = $"CoLearnX credits x{package.Credits}";
        var orderId = await payPal.CreateOrderAsync(amount, options.Currency, customId, description, request.ReturnUrl, ct);

        db.PaymentTransactions.Add(new PaymentTransaction
        {
            UserId = userId,
            CreditPackageId = package.Id,
            Provider = "PayPal",
            ProviderReference = orderId,
            AmountAudCents = package.PayAudCents,
            CreditsGranted = package.Credits,
            Status = PaymentStatus.Pending,
        });
        await db.SaveChangesAsync(ct);

        return new CreatePayPalOrderResponse(orderId, package.Id, amount, options.Currency);
    }

    public async Task<CreditLedgerItemDto> CapturePayPalOrderAsync(int userId, CapturePayPalOrderRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
            throw new InvalidOperationException("OrderId is required.");

        var completed = await FindCompletedCaptureAsync(userId, request.OrderId, ct);
        if (completed is not null)
            return completed;

        _ = await db.PaymentTransactions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ProviderReference == request.OrderId && p.Provider == "PayPal", ct)
            ?? throw new InvalidOperationException("Payment order not found.");

        var (ok, status, captureId) = await payPal.CaptureOrderAsync(request.OrderId, ct);
        if (!ok)
            throw new InvalidOperationException($"PayPal capture status was {status}.");

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            completed = await FindCompletedCaptureAsync(userId, request.OrderId, ct);
            if (completed is not null)
            {
                await tx.CommitAsync(ct);
                return completed;
            }

            var payment = await db.PaymentTransactions
                .FirstOrDefaultAsync(p => p.UserId == userId && p.ProviderReference == request.OrderId && p.Provider == "PayPal", ct)
                ?? throw new InvalidOperationException("Payment order not found.");

            if (payment.Status == PaymentStatus.Completed)
            {
                await tx.CommitAsync(ct);
                return completed ?? new CreditLedgerItemDto(
                    0,
                    payment.CreatedAt,
                    CreditTransactionType.TopUp.ToString(),
                    "PayPal capture already completed.",
                    payment.CreditsGranted,
                    0);
            }

            var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
            user.CreditBalance += payment.CreditsGranted;
            payment.Status = PaymentStatus.Completed;

            var ledger = new CreditTransaction
            {
                UserId = userId,
                Type = CreditTransactionType.TopUp,
                Description = string.IsNullOrWhiteSpace(captureId)
                    ? $"PayPal package ${payment.AmountAudCents / 100m:0} → +{payment.CreditsGranted}"
                    : $"PayPal package ${payment.AmountAudCents / 100m:0} → +{payment.CreditsGranted} (capture {captureId})",
                Delta = payment.CreditsGranted,
                BalanceAfter = user.CreditBalance,
                HeldAfter = user.HeldCredits,
                RelatedPaymentId = payment.Id,
            };
            db.CreditTransactions.Add(ledger);

            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Code = "N-topup",
                Title = "Credits topped up",
                Body = $"+{payment.CreditsGranted} credits via PayPal.",
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new CreditLedgerItemDto(ledger.Id, ledger.CreatedAt, ledger.Type.ToString(), ledger.Description, ledger.Delta, ledger.BalanceAfter, ledger.HeldAfter);
        });
    }

    private async Task<CreditLedgerItemDto?> FindCompletedCaptureAsync(int userId, string orderId, CancellationToken ct)
    {
        var payment = await db.PaymentTransactions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ProviderReference == orderId && p.Provider == "PayPal"
                && p.Status == PaymentStatus.Completed, ct);
        if (payment is null)
            return null;

        var existing = await db.CreditTransactions.AsNoTracking()
            .Where(t => t.RelatedPaymentId == payment.Id)
            .OrderByDescending(t => t.Id)
            .FirstOrDefaultAsync(ct);
        return existing is null
            ? null
            : new CreditLedgerItemDto(existing.Id, existing.CreatedAt, existing.Type.ToString(), existing.Description, existing.Delta, existing.BalanceAfter, existing.HeldAfter);
    }
}
