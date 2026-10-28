using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IStaffCourseService
{
    Task<CreatorCourseOptionsDto> OptionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CreatorCourseDto>> ListAsync(int staffAccountId, CancellationToken ct = default);
    Task<CreatorCourseDto> GetAsync(int staffAccountId, int courseId, CancellationToken ct = default);
    Task<CreatorCourseDto> CreateAsync(int staffAccountId, CreateCreatorCourseRequest request, CancellationToken ct = default);
    Task<CreatorCourseDto> UpdateAsync(int staffAccountId, int courseId, UpdateCreatorCourseRequest request, CancellationToken ct = default);
    Task<CreatorCourseDto> SubmitAsync(int staffAccountId, int courseId, CancellationToken ct = default);
}

public sealed class StaffCourseService(CoLearnXDbContext db) : IStaffCourseService
{
    public async Task<CreatorCourseOptionsDto> OptionsAsync(CancellationToken ct = default)
        => new(
            await db.CourseLevels.AsNoTracking().OrderBy(item => item.SortOrder)
                .Select(item => new CourseOptionDto(item.Id, item.Name)).ToListAsync(ct),
            await db.LearningPaths.AsNoTracking().OrderBy(item => item.Name)
                .Select(item => new CourseOptionDto(item.Id, item.Name)).ToListAsync(ct));

    public async Task<IReadOnlyList<CreatorCourseDto>> ListAsync(int staffAccountId, CancellationToken ct = default)
    {
        var account = await RequireStaffAsync(staffAccountId, ct);
        var courses = await Query().Where(item => item.IsPlatformOwned && item.TrainerId == account.UserId)
            .OrderByDescending(item => item.CreatedAt).ToListAsync(ct);
        return courses.Select(Map).ToList();
    }

    public async Task<CreatorCourseDto> GetAsync(int staffAccountId, int courseId, CancellationToken ct = default)
        => Map(await RequireOwnedCourseAsync(staffAccountId, courseId, tracked: false, ct));

    public async Task<CreatorCourseDto> CreateAsync(
        int staffAccountId,
        CreateCreatorCourseRequest request,
        CancellationToken ct = default)
    {
        var account = await RequireStaffAsync(staffAccountId, ct);
        var creator = await db.Users.SingleAsync(item => item.Email == PlatformIdentity.Email, ct);
        var (code, level, path) = await ValidateInputAsync(null, request.Code, request.CourseLevelId, request.LearningPathId, ct);
        var course = new Course
        {
            CreatorId = creator.Id,
            Creator = creator,
            TrainerId = account.UserId,
            IsPlatformOwned = true,
            Status = CourseStatus.Draft,
        };
        Apply(course, code, request.Title, request.Description, request.Category, request.CreditCost,
            request.LearningOutcomes, level, path);
        course.Interests = await ValidateInterestsAsync(request.InterestIds, ct);
        db.Courses.Add(course);
        db.CourseTrainers.Add(new CourseTrainer
        {
            Course = course,
            TrainerId = account.UserId,
            InvitedByUserId = creator.Id,
            Status = CourseTrainerStatus.Accepted,
            RespondedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        AppendAudit(staffAccountId, course, "PlatformCourseCreated", CourseStatus.Draft.ToString());
        await db.SaveChangesAsync(ct);
        return Map(course);
    }

    public async Task<CreatorCourseDto> UpdateAsync(
        int staffAccountId,
        int courseId,
        UpdateCreatorCourseRequest request,
        CancellationToken ct = default)
    {
        var course = await RequireOwnedCourseAsync(staffAccountId, courseId, tracked: true, ct);
        EnsureEditable(course);
        var (code, level, path) = await ValidateInputAsync(courseId, request.Code, request.CourseLevelId, request.LearningPathId, ct);
        db.CourseLearningOutcomes.RemoveRange(course.LearningOutcomes);
        Apply(course, code, request.Title, request.Description, request.Category, request.CreditCost,
            request.LearningOutcomes, level, path);
        if (request.InterestIds is not null)
        {
            db.CourseInterests.RemoveRange(course.Interests);
            course.Interests = await ValidateInterestsAsync(request.InterestIds, ct);
        }
        AppendAudit(staffAccountId, course, "PlatformCourseUpdated", course.Status.ToString());
        await db.SaveChangesAsync(ct);
        return Map(course);
    }

    public async Task<CreatorCourseDto> SubmitAsync(int staffAccountId, int courseId, CancellationToken ct = default)
    {
        var course = await RequireOwnedCourseAsync(staffAccountId, courseId, tracked: true, ct);
        EnsureEditable(course);
        if (course.Interests.Count is < 1 or > 4)
            throw new CourseException("COURSE_INTERESTS_REQUIRED", "Choose one to four leaf interests before submitting.");
        course.Status = CourseStatus.PendingApproval;
        AppendAudit(staffAccountId, course, "PlatformCourseSubmitted", CourseStatus.PendingApproval.ToString());
        await db.SaveChangesAsync(ct);
        return Map(course);
    }

    private async Task<StaffAccount> RequireStaffAsync(int staffAccountId, CancellationToken ct)
        => await db.StaffAccounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == staffAccountId && item.IsActive && item.User.IsActive, ct)
            ?? throw new CourseException("STAFF_REQUIRED", "An active staff account is required.", 403);

    private async Task<Course> RequireOwnedCourseAsync(int staffAccountId, int courseId, bool tracked, CancellationToken ct)
    {
        var account = await RequireStaffAsync(staffAccountId, ct);
        var query = Query();
        if (!tracked) query = query.AsNoTracking();
        return await query.SingleOrDefaultAsync(item => item.Id == courseId
                && item.IsPlatformOwned && item.TrainerId == account.UserId, ct)
            ?? throw new CourseException("COURSE_NOT_FOUND", "Course was not found.", 404);
    }

    private IQueryable<Course> Query() => db.Courses
        .Include(item => item.Creator)
        .Include(item => item.CourseLevel)
        .Include(item => item.LearningPath)
        .Include(item => item.LearningOutcomes)
        .Include(item => item.Interests).ThenInclude(item => item.Interest);

    private async Task<(string Code, CourseLevel Level, LearningPath Path)> ValidateInputAsync(
        int? courseId, string requestedCode, int levelId, int pathId, CancellationToken ct)
    {
        var code = requestedCode.Trim();
        if (await db.Courses.AnyAsync(item => item.Id != courseId && item.Code.ToUpper() == code.ToUpper(), ct))
            throw new CourseException("COURSE_CODE_EXISTS", "Course code is already in use.", 409, "code");
        var level = await db.CourseLevels.FindAsync([levelId], ct)
            ?? throw new CourseException("COURSE_LEVEL_NOT_FOUND", "Select a valid Course level.", 400, "courseLevelId");
        var path = await db.LearningPaths.FindAsync([pathId], ct)
            ?? throw new CourseException("LEARNING_PATH_NOT_FOUND", "Select a valid Learning Path.", 400, "learningPathId");
        return (code, level, path);
    }

    private async Task<ICollection<CourseInterest>> ValidateInterestsAsync(IReadOnlyList<int>? requested, CancellationToken ct)
    {
        var ids = (requested ?? []).Distinct().ToArray();
        if (ids.Length > 4 || ids.Length != (requested?.Count ?? 0))
            throw new CourseException("INTEREST_LIMIT", "Choose up to four distinct leaf interests.");
        var valid = await db.Interests.CountAsync(item => ids.Contains(item.Id) && item.IsActive && item.ParentId != null, ct);
        if (valid != ids.Length)
            throw new CourseException("INTEREST_NOT_FOUND", "Choose active leaf interests.");
        return ids.Select(id => new CourseInterest { InterestId = id }).ToList();
    }

    private static void Apply(Course course, string code, string title, string? description, string? category,
        int creditCost, IReadOnlyList<string>? outcomes, CourseLevel level, LearningPath path)
    {
        course.Code = code;
        course.Title = title.Trim();
        course.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        course.Category = category?.Trim() ?? string.Empty;
        course.CreditCost = creditCost;
        course.CourseLevelId = level.Id;
        course.CourseLevel = level;
        course.Level = level.Name;
        course.LearningPathId = path.Id;
        course.LearningPath = path;
        course.LearningOutcomes = (outcomes ?? []).Where(item => !string.IsNullOrWhiteSpace(item))
            .Select((item, index) => new CourseLearningOutcome { SortOrder = index + 1, Text = item.Trim() }).ToList();
    }

    private static void EnsureEditable(Course course)
    {
        if (course.Status is not (CourseStatus.Draft or CourseStatus.Rejected))
            throw new CourseException("COURSE_NOT_EDITABLE", $"A Course in {course.Status} status cannot be edited or submitted.", 409);
    }

    private void AppendAudit(int staffAccountId, Course course, string action, string result)
        => db.AuditLogs.Add(new AuditLog
        {
            StaffAccountId = staffAccountId,
            Action = action,
            EntityType = nameof(Course),
            EntityId = course.Id.ToString(),
            Result = result,
        });

    private static CreatorCourseDto Map(Course course) => new(
        course.Id, course.Code, course.Title, course.Description, course.CreatorId,
        course.Creator.Email, PlatformIdentity.Name, course.CourseLevelId ?? 0,
        course.CourseLevel?.Name ?? course.Level, course.LearningPathId ?? 0,
        course.LearningPath?.Name ?? "Unassigned", course.Category, course.CreditCost,
        course.Status.ToString(), course.LearningOutcomes.OrderBy(item => item.SortOrder).Select(item => item.Text).ToList(),
        course.CreatedAt, null, course.Interests.Select(item => item.InterestId).ToList(), true);
}
