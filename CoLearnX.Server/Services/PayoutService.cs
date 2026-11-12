using System.Data;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IPayoutService
{
    Task<PayoutRequestDto> RequestAsync(int userId, string? payoutMethod, CancellationToken ct = default);
    Task<IReadOnlyList<AdminPayoutDto>> ListAsync(PayoutStatus? status, CancellationToken ct = default);
    Task<AdminPayoutDto> ReviewAsync(int adminAccountId, int payoutId, PayoutStatus decision, string? reference,
        CancellationToken ct = default);
}

public sealed class PayoutService(CoLearnXDbContext db) : IPayoutService
{
    public async Task<PayoutRequestDto> RequestAsync(int userId, string? payoutMethod, CancellationToken ct = default)
    {
        var normalized = payoutMethod?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 256)
            throw new LaterPhaseException("PAYOUT_METHOD_REQUIRED", "Enter a payout method up to 256 characters.",
                StatusCodes.Status400BadRequest, "method");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await EarningsLedger.ReleaseDueAsync(db, DateTime.UtcNow, ct);
            if (await db.PayoutRequests.AnyAsync(x => x.UserId == userId && x.Status == PayoutStatus.Requested, ct))
                throw new LaterPhaseException("PAYOUT_ALREADY_REQUESTED", "A payout request is already awaiting review.", 409);
            var rows = await db.Earnings.Where(x => x.UserId == userId && x.Status == EarningStatus.Available).ToListAsync(ct);
            var credits = rows.Sum(x => x.Credits);
            if (credits < EarningsRules.MinimumPayoutCredits)
                throw new LaterPhaseException("PAYOUT_BELOW_MINIMUM",
                    $"At least {EarningsRules.MinimumPayoutCredits} available credits are required.", 409);
            var payout = new PayoutRequest
            {
                UserId = userId, Credits = credits, PayoutMethod = normalized,
                Status = PayoutStatus.Requested, RequestedAt = DateTime.UtcNow
            };
            db.PayoutRequests.Add(payout);
            foreach (var row in rows) { row.Status = EarningStatus.Requested; row.PayoutRequest = payout; }
            await db.SaveChangesAsync(ct);
            db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "PayoutRequested", EntityType = nameof(PayoutRequest),
                EntityId = payout.Id.ToString(), Result = "Succeeded", Reason = $"{credits} credits requested." });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToDto(payout);
        });
    }

    public async Task<IReadOnlyList<AdminPayoutDto>> ListAsync(PayoutStatus? status, CancellationToken ct = default)
    {
        var rows = await db.PayoutRequests.AsNoTracking().Include(x => x.User).ThenInclude(x => x.Roles)
            .Include(x => x.Earnings).Where(x => status == null || x.Status == status)
            .OrderBy(x => x.Status).ThenBy(x => x.RequestedAt).ToListAsync(ct);
        return rows.Select(ToAdminDto).ToList();
    }

    public async Task<AdminPayoutDto> ReviewAsync(int adminAccountId, int payoutId, PayoutStatus decision, string? reference,
        CancellationToken ct = default)
    {
        if (decision == PayoutStatus.Requested)
            throw new LaterPhaseException("INVALID_PAYOUT_DECISION", "Decision must be Paid or Rejected.", 400, "decision");
        var normalized = reference?.Trim();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var payout = await db.PayoutRequests.Include(x => x.User).ThenInclude(x => x.Roles).Include(x => x.Earnings)
                .SingleOrDefaultAsync(x => x.Id == payoutId, ct)
                ?? throw new LaterPhaseException("PAYOUT_NOT_FOUND", "Payout request was not found.", 404);
            if (payout.Status != PayoutStatus.Requested)
            {
                if (payout.Status == decision) return ToAdminDto(payout);
                throw new LaterPhaseException("PAYOUT_ALREADY_REVIEWED", "This payout request has already been reviewed.", 409);
            }
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 256)
                throw new LaterPhaseException("PAYOUT_REFERENCE_REQUIRED", "Enter an offline payment reference or rejection reason.",
                    400, "reference");
            payout.Status = decision;
            payout.Reference = normalized;
            payout.ReviewedAt = DateTime.UtcNow;
            payout.ReviewedByAdminAccountId = adminAccountId;
            foreach (var earning in payout.Earnings.ToList())
            {
                earning.Status = decision == PayoutStatus.Paid ? EarningStatus.Paid : EarningStatus.Available;
                if (decision == PayoutStatus.Rejected) { earning.PayoutRequestId = null; earning.PayoutRequest = null; }
            }
            db.Notifications.Add(new Notification { UserId = payout.UserId, EmailPending = true,
                Code = decision == PayoutStatus.Paid ? "N-payout-paid" : "N-payout-rejected",
                Title = decision == PayoutStatus.Paid ? "Payout recorded" : "Payout request rejected",
                Body = decision == PayoutStatus.Paid
                    ? $"Your {payout.Credits}-credit payout was recorded with reference {normalized}."
                    : $"Your payout request was rejected: {normalized}" });
            db.AuditLogs.Add(new AuditLog { AdminAccountId = adminAccountId,
                Action = decision == PayoutStatus.Paid ? "PayoutPaid" : "PayoutRejected",
                EntityType = nameof(PayoutRequest), EntityId = payout.Id.ToString(), Result = "Succeeded", Reason = normalized });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToAdminDto(payout);
        });
    }

    private static PayoutRequestDto ToDto(PayoutRequest payout) => new(payout.Id, payout.Credits,
        payout.PayoutMethod, payout.Status.ToString(), payout.RequestedAt, payout.ReviewedAt, payout.Reference);

    private static AdminPayoutDto ToAdminDto(PayoutRequest payout)
    {
        var roles = payout.Earnings.Select(x => x.Role.ToString()).Distinct().ToList();
        if (roles.Count == 0)
            roles = payout.User.Roles.Where(x => x.Role is AppRole.Trainer or AppRole.Creator)
                .Select(x => x.Role.ToString()).Distinct().ToList();
        return new AdminPayoutDto(payout.Id, payout.UserId, payout.User.FullName, payout.User.Email,
            roles.Count == 1 ? roles[0] : string.Join(", ", roles), payout.Credits, payout.PayoutMethod, payout.Status.ToString(),
            payout.RequestedAt, payout.ReviewedAt, payout.Reference);
    }
}
