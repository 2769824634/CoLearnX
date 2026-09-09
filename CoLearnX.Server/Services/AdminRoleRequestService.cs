using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IAdminRoleRequestService
{
    Task<IReadOnlyList<AdminRoleRequestDto>> ListAsync(
        RoleRequestStatus? status,
        CancellationToken ct = default);

    Task<AdminRoleRequestReviewResultDto> ReviewAsync(
        int adminAccountId,
        int roleRequestId,
        AdminReviewRequest request,
        CancellationToken ct = default);
}

public sealed class AdminRoleRequestService(
    CoLearnXDbContext db,
    IAuditLogService auditLogs) : IAdminRoleRequestService
{
    public async Task<IReadOnlyList<AdminRoleRequestDto>> ListAsync(
        RoleRequestStatus? status,
        CancellationToken ct = default)
    {
        var query = db.RoleRequests
            .AsNoTracking()
            .Include(roleRequest => roleRequest.User)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(roleRequest => roleRequest.Status == status.Value);

        var roleRequests = await query
            .OrderBy(roleRequest => roleRequest.Status == RoleRequestStatus.Pending ? 0 : 1)
            .ThenBy(roleRequest => roleRequest.CreatedAt)
            .ToListAsync(ct);

        return roleRequests.Select(ToDto).ToList();
    }

    public async Task<AdminRoleRequestReviewResultDto> ReviewAsync(
        int adminAccountId,
        int roleRequestId,
        AdminReviewRequest request,
        CancellationToken ct = default)
    {
        var decision = AdminReviewDecisionParser.Parse(request.Decision);
        var targetStatus = decision == AdminReviewDecision.Approve
            ? RoleRequestStatus.Approved
            : RoleRequestStatus.Rejected;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var roleRequest = await db.RoleRequests
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == roleRequestId, ct)
            ?? throw new KeyNotFoundException("Role request was not found.");

        if (roleRequest.RequestedRole is not (AppRole.Trainer or AppRole.Creator))
            throw new AdminReviewValidationException(
                "ROLE_NOT_REQUESTABLE",
                "Only Trainer and Creator roles can be reviewed.");

        if (roleRequest.Status != RoleRequestStatus.Pending)
        {
            if (roleRequest.Status == targetStatus)
                return new AdminRoleRequestReviewResultDto(ToDto(roleRequest), true);

            throw new AdminReviewConflictException(
                "ROLE_REQUEST_ALREADY_REVIEWED",
                $"This request has already been {roleRequest.Status.ToString().ToLowerInvariant()}.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (decision == AdminReviewDecision.Reject && reason is null)
            throw new AdminReviewValidationException(
                "REJECTION_REASON_REQUIRED",
                "A reason is required when rejecting a role request.");

        if (decision == AdminReviewDecision.Approve && !roleRequest.User.IsActive)
            throw new AdminReviewConflictException(
                "ROLE_REQUEST_USER_INACTIVE",
                "An inactive user cannot be granted a requested role.");

        if (decision == AdminReviewDecision.Approve)
        {
            var alreadyHasRole = await db.UserRoles.AnyAsync(
                userRole => userRole.UserId == roleRequest.UserId
                    && userRole.Role == roleRequest.RequestedRole,
                ct);
            if (!alreadyHasRole)
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = roleRequest.UserId,
                    Role = roleRequest.RequestedRole,
                    IsVisible = true,
                });
            }
        }

        roleRequest.Status = targetStatus;
        roleRequest.ReviewNote = reason;
        roleRequest.ReviewedByAdminAccountId = adminAccountId;
        roleRequest.ReviewedAt = DateTime.UtcNow;

        await auditLogs.AppendAdminAsync(
            adminAccountId,
            decision == AdminReviewDecision.Approve
                ? "RoleRequestApproved"
                : "RoleRequestRejected",
            nameof(RoleRequest),
            roleRequest.Id.ToString(),
            targetStatus.ToString(),
            reason,
            ct);
        await transaction.CommitAsync(ct);

        return new AdminRoleRequestReviewResultDto(ToDto(roleRequest), false);
    }

    private static AdminRoleRequestDto ToDto(RoleRequest roleRequest)
        => new(
            roleRequest.Id,
            roleRequest.UserId,
            roleRequest.User.Email,
            roleRequest.User.FullName,
            roleRequest.RequestedRole.ToString(),
            roleRequest.Status.ToString(),
            roleRequest.DegreeOrResumePath,
            roleRequest.IdDocumentPath,
            roleRequest.ReviewNote,
            roleRequest.ReviewedByAdminAccountId,
            roleRequest.CreatedAt,
            roleRequest.ReviewedAt);

}
