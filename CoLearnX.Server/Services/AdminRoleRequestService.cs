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
    IAuditLogService auditLogs,
    INotificationRealtime? realtime = null) : IAdminRoleRequestService
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

        var names = await RoleRequestService.SpecialtyNamesAsync(db, ct);
        return roleRequests.Select(item => ToDto(item, names)).ToList();
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

        // Azure SQL enables retry-on-failure; user transactions must run inside that strategy.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
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
                    return new AdminRoleRequestReviewResultDto(ToDto(roleRequest, await RoleRequestService.SpecialtyNamesAsync(db, ct)), true);

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
                var existingRole = await db.UserRoles.SingleOrDefaultAsync(
                    userRole => userRole.UserId == roleRequest.UserId
                        && userRole.Role == roleRequest.RequestedRole,
                    ct);
                // Approval only certifies the account's own role; it never adds a second role.
                if (existingRole is null)
                    throw new AdminReviewConflictException(
                        "ROLE_NOT_HELD",
                        "This account does not hold the requested role. Each account holds one role.");
                existingRole.CertifiedAt = DateTime.UtcNow;

                var specialtyIds = RoleRequestService.ParseSpecialtyIds(roleRequest.SpecialtyIds);
                if (specialtyIds.Count > 0)
                {
                    db.UserSpecialties.RemoveRange(await db.UserSpecialties
                        .Where(item => item.UserId == roleRequest.UserId).ToListAsync(ct));
                    db.UserSpecialties.AddRange(specialtyIds.Select(id =>
                        new UserSpecialty { UserId = roleRequest.UserId, CourseSubjectId = id }));
                }
            }

            roleRequest.Status = targetStatus;
            roleRequest.ReviewNote = reason;
            roleRequest.ReviewedByAdminAccountId = adminAccountId;
            roleRequest.ReviewedAt = DateTime.UtcNow;
            var role = roleRequest.RequestedRole.ToString();
            var notification = decision == AdminReviewDecision.Approve
                ? new Notification
                {
                    UserId = roleRequest.UserId, Code = "N-role-approved", Title = "Role request approved",
                    Body = $"Your {role} access is approved. Switch to it from the account menu.",
                }
                : new Notification
                {
                    UserId = roleRequest.UserId, Code = "N-role-rejected", Title = "Role request not approved",
                    Body = $"Your {role} request was not approved: {reason}",
                };
            db.Notifications.Add(notification);

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
            if (realtime is not null)
                await realtime.PublishAsync(notification, ct);

            return new AdminRoleRequestReviewResultDto(ToDto(roleRequest, await RoleRequestService.SpecialtyNamesAsync(db, ct)), false);
        });
    }

    private static AdminRoleRequestDto ToDto(RoleRequest roleRequest, IReadOnlyDictionary<int, string> names)
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
            roleRequest.ReviewedAt,
            roleRequest.ApplicantStatement,
            RoleRequestService.SpecialtyNames(roleRequest.SpecialtyIds, names));

}
