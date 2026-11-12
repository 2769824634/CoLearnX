using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
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
            string? specialtyChange = null;
            IReadOnlyList<string> certifiedNames = [];
            if (decision == AdminReviewDecision.Reject && reason is null)
                throw new AdminReviewValidationException(
                    "REJECTION_REASON_REQUIRED",
                    "A reason is required when rejecting a role request.");

            if (decision == AdminReviewDecision.Approve && !roleRequest.User.IsActive)
                throw new AdminReviewConflictException(
                    "ROLE_REQUEST_USER_INACTIVE",
                    "An inactive user cannot be granted a requested role.");

            if (decision == AdminReviewDecision.Approve
                && await db.UserRoles.AnyAsync(item => item.UserId == roleRequest.UserId && item.Role == AppRole.Member, ct))
                throw new AdminReviewConflictException(
                    "ROLE_NOT_REQUESTABLE",
                    "Member accounts cannot apply for Trainer or Creator roles. Reject this request instead.");

            if (decision == AdminReviewDecision.Approve)
            {
                var existingRole = await db.UserRoles.SingleOrDefaultAsync(
                    userRole => userRole.UserId == roleRequest.UserId
                        && userRole.Role == roleRequest.RequestedRole,
                    ct);
                if (existingRole is null)
                    throw new AdminReviewConflictException(
                        "ROLE_NOT_REQUESTABLE",
                        "Accounts can only be certified for an existing role. Reject this request instead.");
                existingRole.CertifiedAt = DateTime.UtcNow;

                var specialtyIds = RoleRequestService.ParseSpecialtyIds(roleRequest.SpecialtyIds);
                // The admin may correct the applicant's choice. The corrected set is what gets certified and stored.
                if (request.SpecialtyIds is not null)
                {
                    var corrected = await ValidateCorrectionAsync(request.SpecialtyIds, ct);
                    if (!corrected.OrderBy(id => id).SequenceEqual(specialtyIds.OrderBy(id => id)))
                    {
                        var names = await RoleRequestService.SpecialtyNamesAsync(db, ct);
                        string List(IEnumerable<int> ids) => string.Join(", ", ids.Where(names.ContainsKey).Select(id => names[id]));
                        specialtyChange = $"Specialties changed from [{List(specialtyIds)}] to [{List(corrected)}].";
                        roleRequest.SpecialtyIds = string.Join(',', corrected);
                        specialtyIds = corrected;
                    }
                }
                if (specialtyIds.Count > 0)
                {
                    certifiedNames = RoleRequestService.SpecialtyNames(string.Join(',', specialtyIds),
                        await RoleRequestService.SpecialtyNamesAsync(db, ct)) ?? [];
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
                    Body = certifiedNames.Count == 0
                        ? $"Your {role} certification is approved. Your workspace is now open."
                        : $"Your {role} certification is approved for {string.Join(", ", certifiedNames)}. Your workspace is now open.",
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
                AuditReason(reason, specialtyChange),
                ct);
            await transaction.CommitAsync(ct);
            if (realtime is not null)
                await realtime.PublishAsync(notification, ct);

            return new AdminRoleRequestReviewResultDto(ToDto(roleRequest, await RoleRequestService.SpecialtyNamesAsync(db, ct)), false);
        });
    }

    private async Task<IReadOnlyList<int>> ValidateCorrectionAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var chosen = ids.Distinct().ToList();
        if (chosen.Count == 0)
            throw new AdminReviewValidationException("SPECIALTIES_REQUIRED", "Choose at least one specialty to certify.");
        if (chosen.Count > RoleRequestService.MaxSpecialties)
            throw new AdminReviewValidationException("TOO_MANY_SPECIALTIES",
                $"Certify at most {RoleRequestService.MaxSpecialties} specialties.");
        var known = await db.CourseSubjects.CountAsync(subject => chosen.Contains(subject.Id), ct);
        if (known != chosen.Count)
            throw new AdminReviewValidationException("SPECIALTY_NOT_FOUND", "One of the chosen specialties does not exist.");
        return chosen;
    }

    // The audit reason holds 512 characters, so a long specialty change is shortened rather than rejected.
    private static string? AuditReason(string? reason, string? specialtyChange)
    {
        var text = specialtyChange is null ? reason : reason is null ? specialtyChange : $"{reason} {specialtyChange}";
        return text is { Length: > 512 } ? text[..512] : text;
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
            RoleRequestService.SpecialtyNames(roleRequest.SpecialtyIds, names),
            RoleRequestService.ParseSpecialtyIds(roleRequest.SpecialtyIds),
            RoleRequestFiles.SplitKeys(roleRequest.DegreeOrResumePath).Count,
            RoleRequestFiles.SplitKeys(roleRequest.IdDocumentPath).Count);

}
