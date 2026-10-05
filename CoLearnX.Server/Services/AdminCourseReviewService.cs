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
            .Include(course => course.Creator)
            .Include(course => course.LearningPath)
            .Include(course => course.LearningOutcomes)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(course => course.Status == status.Value);

        var courses = await query
            .OrderBy(course => course.Status == CourseStatus.PendingApproval ? 0 : 1)
            .ThenBy(course => course.CreatedAt)
            .ToListAsync(ct);

        if (courses.Count == 0)
            return [];

        var courseIds = courses.Select(course => course.Id).ToArray();
        var details = await LoadDetailsAsync(courseIds, ct);
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
                latestReviewByCourseId.GetValueOrDefault(course.Id.ToString()),
                details.InterestsByCourseId.GetValueOrDefault(course.Id) ?? [],
                details.MaterialsByCourseId.GetValueOrDefault(course.Id)))
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

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var course = await db.Courses
                .Include(item => item.Trainer)
                .Include(item => item.Creator)
                .Include(item => item.LearningPath)
                .Include(item => item.LearningOutcomes)
                .SingleOrDefaultAsync(item => item.Id == courseId, ct)
                ?? throw new KeyNotFoundException("Course was not found.");

            var existingReview = await FindLatestReviewLogAsync(course.Id, ct);
            if (course.Status != CourseStatus.PendingApproval)
            {
                if (course.Status == targetStatus && existingReview?.Action == targetAction)
                {
                    var existingDetails = await LoadDetailsAsync([course.Id], ct);
                    return new AdminCourseReviewResultDto(ToDto(
                        course,
                        existingReview,
                        existingDetails.InterestsByCourseId.GetValueOrDefault(course.Id) ?? [],
                        existingDetails.MaterialsByCourseId.GetValueOrDefault(course.Id)), true);
                }

                throw new AdminReviewConflictException(
                    "COURSE_NOT_PENDING_APPROVAL",
                    $"A course in {course.Status} status cannot be reviewed.");
            }

            var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            if (decision == AdminReviewDecision.Reject && reason is null)
                throw new AdminReviewValidationException(
                    "REJECTION_REASON_REQUIRED",
                    "A reason is required when rejecting a course.");
            if (decision == AdminReviewDecision.Approve
                && await db.CourseInterests.CountAsync(i => i.CourseId == course.Id, ct) is < 1 or > 4)
                throw new AdminReviewValidationException(
                    "COURSE_INTERESTS_REQUIRED", "A Course needs one to four leaf interests before publication.");

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
            var details = await LoadDetailsAsync([course.Id], ct);
            return new AdminCourseReviewResultDto(ToDto(
                course,
                reviewLog,
                details.InterestsByCourseId.GetValueOrDefault(course.Id) ?? [],
                details.MaterialsByCourseId.GetValueOrDefault(course.Id)), false);
        });
    }

    private async Task<CourseReviewDetails> LoadDetailsAsync(
        IReadOnlyCollection<int> courseIds,
        CancellationToken ct)
    {
        if (courseIds.Count == 0)
            return CourseReviewDetails.Empty;

        var interestRows = await db.CourseInterests
            .AsNoTracking()
            .Where(item => courseIds.Contains(item.CourseId))
            .OrderBy(item => item.CourseId)
            .ThenBy(item => item.Interest.SortOrder)
            .ThenBy(item => item.Interest.Name)
            .Select(item => new
            {
                item.CourseId,
                item.InterestId,
                item.Interest.Slug,
                item.Interest.Name,
            })
            .ToListAsync(ct);

        var materialRows = await (
            from link in db.CourseMaterials.AsNoTracking()
            join version in db.CourseMaterialVersions.AsNoTracking()
                on link.LearningMaterialId equals version.LearningMaterialId
            where courseIds.Contains(link.CourseId)
            orderby link.CourseId, version.LearningMaterial.Title, version.VersionNumber, version.Id
            select new
            {
                link.CourseId,
                version.Id,
                version.LearningMaterialId,
                Title = version.LearningMaterial.Title,
                version.Format,
                version.VersionNumber,
                version.Status,
            }).ToListAsync(ct);

        var interestsByCourseId = interestRows
            .GroupBy(item => item.CourseId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CourseInterestDto>)group
                    .Select(item => new CourseInterestDto(item.InterestId, item.Slug, item.Name))
                    .ToList());
        var materialsByCourseId = materialRows
            .GroupBy(item => item.CourseId)
            .ToDictionary(
                group => group.Key,
                group => BuildMaterialSummary(group.Select(item => new MaterialRow(
                    item.Id,
                    item.LearningMaterialId,
                    item.Title,
                    item.Format,
                    item.VersionNumber,
                    item.Status))));

        return new CourseReviewDetails(interestsByCourseId, materialsByCourseId);
    }

    private Task<AuditLog?> FindLatestReviewLogAsync(int courseId, CancellationToken ct)
        => db.AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityType == nameof(Course)
                && log.EntityId == courseId.ToString()
                && (log.Action == PublishedAction || log.Action == RejectedAction))
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefaultAsync(ct);

    private static AdminCourseDto ToDto(
        Course course,
        AuditLog? reviewLog,
        IReadOnlyList<CourseInterestDto>? interests = null,
        MaterialSummary? materials = null)
        => new(
            course.Id,
            course.Code,
            course.Title,
            course.Description,
            course.CreatorId,
            course.Creator.Email,
            course.Creator.FullName,
            course.CreditCost,
            course.Level,
            course.Category,
            course.Status.ToString(),
            reviewLog?.Reason,
            reviewLog?.AdminAccountId,
            course.CreatedAt,
            reviewLog?.CreatedAt,
            course.LearningOutcomes.OrderBy(outcome => outcome.SortOrder).Select(outcome => outcome.Text).ToList(),
            course.LearningPath?.Name,
            course.Creator.FullName,
            interests ?? [],
            materials?.Versions.Count ?? 0,
            materials?.StatusCounts ?? EmptyStatusCounts(),
            materials?.Versions ?? []);

    private static MaterialSummary BuildMaterialSummary(IEnumerable<MaterialRow> rows)
    {
        var materialRows = rows.ToList();
        var versions = materialRows
            .Select(item => new AdminCourseMaterialVersionDto(
                item.VersionId,
                item.LearningMaterialId,
                item.Title,
                item.Format,
                item.VersionNumber,
                item.Status.ToString()))
            .ToList();
        var statusCounts = Enum.GetValues<MaterialVersionStatus>()
            .ToDictionary(status => status.ToString(), status => materialRows.Count(item => item.Status == status));
        return new MaterialSummary(versions, statusCounts);
    }

    private static IReadOnlyDictionary<string, int> EmptyStatusCounts()
        => Enum.GetValues<MaterialVersionStatus>()
            .ToDictionary(status => status.ToString(), _ => 0);

    private sealed record MaterialRow(
        int VersionId,
        int LearningMaterialId,
        string Title,
        string Format,
        int VersionNumber,
        MaterialVersionStatus Status);

    private sealed record MaterialSummary(
        IReadOnlyList<AdminCourseMaterialVersionDto> Versions,
        IReadOnlyDictionary<string, int> StatusCounts);

    private sealed record CourseReviewDetails(
        IReadOnlyDictionary<int, IReadOnlyList<CourseInterestDto>> InterestsByCourseId,
        IReadOnlyDictionary<int, MaterialSummary> MaterialsByCourseId)
    {
        public static CourseReviewDetails Empty { get; } = new(
            new Dictionary<int, IReadOnlyList<CourseInterestDto>>(),
            new Dictionary<int, MaterialSummary>());
    }
}
