using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IAdminFinanceService
{
    Task<IReadOnlyList<AdminCreditLedgerItemDto>> GetLedgerAsync(string? search, string? type, CancellationToken ct = default);
    Task<AdminCreditMutationDto> AdjustAsync(int adminAccountId, AdminCreditAdjustmentRequest request, CancellationToken ct = default);
    Task<DisputeDto> CreateDisputeAsync(int userId, CreateDisputeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DisputeDto>> ListMyDisputesAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<DisputeDto>> ListDisputesAsync(string? status, CancellationToken ct = default);
    Task<DisputeDto> ReviewDisputeAsync(int adminAccountId, int disputeId, AdminDisputeReviewRequest request, CancellationToken ct = default);
}

public sealed class AdminFinanceService(CoLearnXDbContext db) : IAdminFinanceService
{
    public async Task<IReadOnlyList<AdminCreditLedgerItemDto>> GetLedgerAsync(string? search, string? type,
        CancellationToken ct = default)
    {
        var query = db.CreditTransactions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<CreditTransactionType>(type, true, out var parsed))
                throw new LaterPhaseException("INVALID_TRANSACTION_TYPE", "Unknown credit transaction type.", field: "type");
            query = query.Where(item => item.Type == parsed);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.User.FullName.Contains(term) || item.User.Email.Contains(term)
                || item.Description.Contains(term));
        }
        return await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => new AdminCreditLedgerItemDto(
                item.Id,
                item.CreatedAt,
                item.UserId,
                item.User.FullName,
                item.User.Email,
                item.Type.ToString(),
                item.Description,
                item.Delta,
                item.BalanceAfter,
                item.RelatedEnrollmentId,
                item.RelatedDisputeId,
                item.AdminAccountId))
            .ToListAsync(ct);
    }

    public async Task<AdminCreditMutationDto> AdjustAsync(int adminAccountId, AdminCreditAdjustmentRequest request,
        CancellationToken ct = default)
    {
        ValidateIdempotencyKey(request.IdempotencyKey);
        if (request.Delta == 0 || request.Delta is < -10000 or > 10000)
            throw new LaterPhaseException("INVALID_ADJUSTMENT", "Credit adjustment must be between -10000 and 10000 and cannot be zero.", field: "delta");
        var reason = RequireReason(request.Reason);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var replay = await db.CreditTransactions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.IdempotencyKey == request.IdempotencyKey, ct);
        if (replay is not null)
        {
            if (replay.Type != CreditTransactionType.AdminAdjustment || replay.UserId != request.UserId
                || replay.Delta != request.Delta || replay.AdminAccountId != adminAccountId || replay.Description != reason)
                throw new LaterPhaseException("IDEMPOTENCY_CONFLICT", "This idempotency key was used for another operation.", 409);
            return ToMutation(replay, true);
        }

        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == request.UserId, ct)
            ?? throw new LaterPhaseException("USER_NOT_FOUND", "User was not found.", 404);
        if (user.CreditBalance + request.Delta < 0)
            throw new LaterPhaseException("NEGATIVE_BALANCE", "The adjustment cannot make the credit balance negative.", 409, "delta");
        user.CreditBalance += request.Delta;
        var ledger = new CreditTransaction
        {
            UserId = user.Id,
            Type = CreditTransactionType.AdminAdjustment,
            Description = reason,
            Delta = request.Delta,
            BalanceAfter = user.CreditBalance,
            AdminAccountId = adminAccountId,
            IdempotencyKey = request.IdempotencyKey,
        };
        db.CreditTransactions.Add(ledger);
        db.AuditLogs.Add(new AuditLog
        {
            AdminAccountId = adminAccountId,
            Action = "CreditAdjusted",
            EntityType = nameof(User),
            EntityId = user.Id.ToString(),
            Result = "Succeeded",
            Reason = $"{request.Delta:+#;-#;0} credits: {reason}",
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToMutation(ledger, false);
    }

    public async Task<DisputeDto> CreateDisputeAsync(int userId, CreateDisputeRequest request, CancellationToken ct = default)
    {
        await RequireActiveMemberAsync(userId, ct);
        var enrollment = await EnrollmentQuery().SingleOrDefaultAsync(item => item.Id == request.EnrollmentId && item.UserId == userId, ct)
            ?? throw new LaterPhaseException("ENROLLMENT_NOT_FOUND", "Enrollment was not found.", 404);
        if (enrollment.Status is EnrollmentStatus.Cancelled or EnrollmentStatus.Refunded)
            throw new LaterPhaseException("DISPUTE_NOT_ALLOWED", "This enrollment cannot be disputed.", 409);
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new LaterPhaseException("INVALID_DISPUTE_REASON", "A reason of up to 1000 characters is required.", field: "reason");
        var existing = await db.Disputes.SingleOrDefaultAsync(item => item.EnrollmentId == enrollment.Id && item.Status == DisputeStatus.Open, ct);
        if (existing is not null)
            return await GetDisputeAsync(existing.Id, false, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var dispute = new Dispute { RaisedByUserId = userId, EnrollmentId = enrollment.Id, Reason = reason };
        db.Disputes.Add(dispute);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "DisputeSubmitted",
            EntityType = nameof(Dispute),
            EntityId = dispute.Id.ToString(),
            Result = "Succeeded",
            Reason = enrollment.Course.Code,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetDisputeAsync(dispute.Id, false, ct);
    }

    public async Task<IReadOnlyList<DisputeDto>> ListMyDisputesAsync(int userId, CancellationToken ct = default)
    {
        await RequireActiveMemberAsync(userId, ct);
        return await DisputeQuery().Where(item => item.RaisedByUserId == userId && item.EnrollmentId != null)
            .OrderByDescending(item => item.CreatedAt)
            .Select(ToDisputeDto()).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DisputeDto>> ListDisputesAsync(string? status, CancellationToken ct = default)
    {
        var query = DisputeQuery().Where(item => item.EnrollmentId != null);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DisputeStatus>(status, true, out var parsed))
                throw new LaterPhaseException("INVALID_STATUS", "Unknown dispute status.", field: "status");
            query = query.Where(item => item.Status == parsed);
        }
        return await query.OrderByDescending(item => item.CreatedAt).Select(ToDisputeDto()).ToListAsync(ct);
    }

    public async Task<DisputeDto> ReviewDisputeAsync(int adminAccountId, int disputeId, AdminDisputeReviewRequest request,
        CancellationToken ct = default)
    {
        ValidateIdempotencyKey(request.IdempotencyKey);
        var refund = request.Decision?.Trim().Equals("Refund", StringComparison.OrdinalIgnoreCase) == true;
        var reject = request.Decision?.Trim().Equals("Reject", StringComparison.OrdinalIgnoreCase) == true;
        if (!refund && !reject)
            throw new LaterPhaseException("INVALID_REVIEW_DECISION", "Decision must be Refund or Reject.", field: "decision");
        var reason = RequireReason(request.Reason);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var dispute = await DisputeQuery().SingleOrDefaultAsync(item => item.Id == disputeId, ct)
            ?? throw new LaterPhaseException("DISPUTE_NOT_FOUND", "Dispute was not found.", 404);
        if (dispute.EnrollmentId is null || dispute.Enrollment is null)
            throw new LaterPhaseException("DISPUTE_NOT_REFUNDABLE", "Only enrollment disputes can be reviewed in this workflow.", 409);
        if (dispute.Status != DisputeStatus.Open)
        {
            var expectedStatus = refund ? DisputeStatus.ResolvedRefund : DisputeStatus.Rejected;
            if (dispute.ResolutionKey != request.IdempotencyKey || dispute.Status != expectedStatus
                || dispute.ResolutionNote != reason || (refund && dispute.RefundCredits != request.RefundCredits))
                throw new LaterPhaseException("DISPUTE_ALREADY_RESOLVED", "The dispute has already been resolved.", 409);
            return await GetDisputeAsync(dispute.Id, true, ct);
        }

        if (refund && (!request.RefundCredits.HasValue || request.RefundCredits <= 0
            || request.RefundCredits > dispute.Enrollment!.CreditsSpent))
            throw new LaterPhaseException("INVALID_REFUND", "Refund credits must be positive and cannot exceed the enrollment cost.", field: "refundCredits");

        dispute.HandledByAdminId = adminAccountId;
        dispute.ResolutionKey = request.IdempotencyKey;
        dispute.ResolutionNote = reason;
        dispute.ResolvedAt = DateTime.UtcNow;
        if (refund)
        {
            var credits = request.RefundCredits!.Value;
            dispute.Status = DisputeStatus.ResolvedRefund;
            dispute.RefundCredits = credits;
            dispute.Enrollment!.User.CreditBalance += credits;
            if (credits == dispute.Enrollment.CreditsSpent)
                dispute.Enrollment.Status = EnrollmentStatus.Refunded;
            db.CreditTransactions.Add(new CreditTransaction
            {
                UserId = dispute.RaisedByUserId,
                Type = CreditTransactionType.Refund,
                Description = $"Refund for dispute #{dispute.Id}: {reason}",
                Delta = credits,
                BalanceAfter = dispute.Enrollment.User.CreditBalance,
                RelatedEnrollmentId = dispute.EnrollmentId,
                RelatedDisputeId = dispute.Id,
                AdminAccountId = adminAccountId,
                IdempotencyKey = request.IdempotencyKey,
            });
            db.Notifications.Add(new Notification
            {
                UserId = dispute.RaisedByUserId,
                Code = "N-09",
                Title = "Refund processed",
                Body = $"{credits} credits were restored for dispute #{dispute.Id}.",
            });
        }
        else
        {
            dispute.Status = DisputeStatus.Rejected;
        }
        db.AuditLogs.Add(new AuditLog
        {
            AdminAccountId = adminAccountId,
            Action = refund ? "DisputeRefunded" : "DisputeRejected",
            EntityType = nameof(Dispute),
            EntityId = dispute.Id.ToString(),
            Result = "Succeeded",
            Reason = reason,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetDisputeAsync(dispute.Id, false, ct);
    }

    private IQueryable<Enrollment> EnrollmentQuery()
        => db.Enrollments.Include(item => item.User).Include(item => item.Course);

    private IQueryable<Dispute> DisputeQuery()
        => db.Disputes.Include(item => item.Enrollment).ThenInclude(enrollment => enrollment!.User)
            .Include(item => item.Enrollment).ThenInclude(enrollment => enrollment!.Course);

    private async Task<DisputeDto> GetDisputeAsync(int id, bool replayed, CancellationToken ct)
    {
        var item = await DisputeQuery().SingleAsync(candidate => candidate.Id == id, ct);
        var balance = item.Status == DisputeStatus.ResolvedRefund ? item.Enrollment!.User.CreditBalance : (int?)null;
        return MapDispute(item, balance, replayed);
    }

    private static System.Linq.Expressions.Expression<Func<Dispute, DisputeDto>> ToDisputeDto()
        => item => new DisputeDto(
            item.Id,
            item.RaisedByUserId,
            item.Enrollment!.User.FullName,
            item.Enrollment.User.Email,
            item.EnrollmentId!.Value,
            item.Enrollment.Course.Code,
            item.Enrollment.Course.Title,
            item.Reason,
            item.Status.ToString(),
            item.Enrollment.CreditsSpent,
            item.RefundCredits,
            item.ResolutionNote,
            item.CreatedAt,
            item.ResolvedAt,
            null,
            false);

    private static DisputeDto MapDispute(Dispute item, int? balance, bool replayed)
        => new(
            item.Id,
            item.RaisedByUserId,
            item.Enrollment!.User.FullName,
            item.Enrollment.User.Email,
            item.EnrollmentId!.Value,
            item.Enrollment.Course.Code,
            item.Enrollment.Course.Title,
            item.Reason,
            item.Status.ToString(),
            item.Enrollment.CreditsSpent,
            item.RefundCredits,
            item.ResolutionNote,
            item.CreatedAt,
            item.ResolvedAt,
            balance,
            replayed);

    private static AdminCreditMutationDto ToMutation(CreditTransaction item, bool replayed)
        => new(item.Id, item.UserId, item.Delta, item.BalanceAfter, item.Type.ToString(), replayed);

    private async Task RequireActiveMemberAsync(int userId, CancellationToken ct)
    {
        var activeMember = await db.Users.AnyAsync(user => user.Id == userId && user.IsActive
            && user.Roles.Any(role => role.Role == AppRole.Member), ct);
        if (!activeMember)
            throw new LaterPhaseException("MEMBER_REQUIRED", "An active Member account is required.", 403);
    }

    private static void ValidateIdempotencyKey(Guid key)
    {
        if (key == Guid.Empty)
            throw new LaterPhaseException("IDEMPOTENCY_KEY_REQUIRED", "A non-empty idempotency key is required.", field: "idempotencyKey");
    }

    private static string RequireReason(string? value)
    {
        var reason = value?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 512)
            throw new LaterPhaseException("REASON_REQUIRED", "A reason of up to 512 characters is required.", field: "reason");
        return reason;
    }
}
