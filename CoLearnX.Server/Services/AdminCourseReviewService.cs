using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IAdminCourseReviewService
{
    Task<IReadOnlyList<AdminCourseDto>> ListAsync(
        CourseStatus? status,
        CancellationToken ct = default);

    Task<AdminCourseReviewResultDto> ReviewAsync(
        int adminAccountId,
        int courseId,
        AdminReviewRequest request,
        CancellationToken ct = default);
}

public sealed class AdminCourseReviewService(
    CoLearnXDbContext db,
    IAuditLogService auditLogs) : IAdminCourseReviewService
{
    private const string PublishedAction = "CoursePublished";
    private const string RejectedAction = "CourseRejected";

    public async Task<IReadOnlyList<AdminCourseDto>> ListAsync(
        CourseStatus? status,
        CancellationToken ct = default)
    {
        var query = db.Courses
            .AsNoTracking()
            .Include(course => course.Trainer)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(course => course.Status == status.Value);

        var courses = await query
            .OrderBy(course => course.Status == CourseStatus.PendingApproval ? 0 : 1)
            .ThenBy(course => course.CreatedAt)
            .ToListAsync(ct);

        if (courses.Count == 0)
            return [];

        var entityIds = courses.Select(course => course.Id.ToString()).ToArray();
        var reviewLogs = await db.AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityType == nameof(Course)
                && log.EntityId != null
                && entityIds.Contains(log.EntityId)
                && (log.Action == PublishedAction || log.Action == RejectedAction))
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync(ct);
        var latestReviewByCourseId = reviewLogs
            .GroupBy(log => log.EntityId!)
            .ToDictionary(group => group.Key, group => group.First());

        return courses
            .Select(course => ToDto(
                course,
                latestReviewByCourseId.GetValueOrDefault(course.Id.ToString())))
            .ToList();
    }

    public async Task<AdminCourseReviewResultDto> ReviewAsync(
        int adminAccountId,
        int courseId,
        AdminReviewRequest request,
        CancellationToken ct = default)
    {
        var decision = AdminReviewDecisionParser.Parse(request.Decision);
        var targetStatus = decision == AdminReviewDecision.Approve
            ? CourseStatus.Published
            : CourseStatus.Rejected;
        var targetAction = decision == AdminReviewDecision.Approve
            ? PublishedAction
            : RejectedAction;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var course = await db.Courses
            .Include(item => item.Trainer)
            .SingleOrDefaultAsync(item => item.Id == courseId, ct)
            ?? throw new KeyNotFoundException("Course was not found.");

        var existingReview = await FindLatestReviewLogAsync(course.Id, ct);
        if (course.Status != CourseStatus.PendingApproval)
        {
            if (course.Status == targetStatus && existingReview?.Action == targetAction)
                return new AdminCourseReviewResultDto(ToDto(course, existingReview), true);

            throw new AdminReviewConflictException(
                "COURSE_NOT_PENDING_APPROVAL",
                $"A course in {course.Status} status cannot be reviewed.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (decision == AdminReviewDecision.Reject && reason is null)
            throw new AdminReviewValidationException(
                "REJECTION_REASON_REQUIRED",
                "A reason is required when rejecting a course.");

        course.Status = targetStatus;
        await auditLogs.AppendAdminAsync(
            adminAccountId,
            targetAction,
            nameof(Course),
            course.Id.ToString(),
            targetStatus.ToString(),
            reason,
            ct);
        await transaction.CommitAsync(ct);

        var reviewLog = await FindLatestReviewLogAsync(course.Id, ct);
        return new AdminCourseReviewResultDto(ToDto(course, reviewLog), false);
    }

    private Task<AuditLog?> FindLatestReviewLogAsync(int courseId, CancellationToken ct)
        => db.AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityType == nameof(Course)
                && log.EntityId == courseId.ToString()
                && (log.Action == PublishedAction || log.Action == RejectedAction))
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefaultAsync(ct);

    private static AdminCourseDto ToDto(Course course, AuditLog? reviewLog)
        => new(
            course.Id,
            course.Code,
            course.Title,
            course.Description,
            course.TrainerId,
            course.Trainer.Email,
            course.Trainer.FullName,
            course.CreditCost,
            course.Level,
            course.Category,
            course.Status.ToString(),
            reviewLog?.Reason,
            reviewLog?.AdminAccountId,
            course.CreatedAt,
            reviewLog?.CreatedAt);
}
