using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ICourseService
{
    Task<IReadOnlyList<CourseListItemDto>> ListAsync(int? userId, string? search, string? category, string? level, bool? featured, CancellationToken ct = default);
    Task<CourseDetailDto?> GetByIdAsync(int courseId, int? userId, CancellationToken ct = default);
    Task<WishlistResultDto> AddToWishlistAsync(int userId, int courseId, CancellationToken ct = default);
    Task<WishlistResultDto> RemoveFromWishlistAsync(int userId, int courseId, CancellationToken ct = default);
    Task<IReadOnlyList<CreatorCourseDto>> ListForCreatorAsync(int creatorUserId, CancellationToken ct = default);
    Task<CreatorCourseOptionsDto> GetCreatorOptionsAsync(int creatorUserId, CancellationToken ct = default);
    Task<CreatorCourseDto> GetForCreatorAsync(int creatorUserId, int courseId, CancellationToken ct = default);
    Task<CreatorCourseDto> CreateForCreatorAsync(int creatorUserId, CreateCreatorCourseRequest request, CancellationToken ct = default);
    Task<CreatorCourseDto> UpdateForCreatorAsync(int creatorUserId, int courseId, UpdateCreatorCourseRequest request, CancellationToken ct = default);
    Task<CreatorCourseDto> SubmitForCreatorAsync(int creatorUserId, int courseId, CancellationToken ct = default);
}

public class CourseService(CoLearnXDbContext db) : ICourseService
{
    public async Task<CreatorCourseOptionsDto> GetCreatorOptionsAsync(
        int creatorUserId,
        CancellationToken ct = default)
    {
        await RequireCreatorAsync(creatorUserId, ct);
        var levels = await db.CourseLevels.AsNoTracking()
            .OrderBy(level => level.SortOrder)
            .ThenBy(level => level.Name)
            .Select(level => new CourseOptionDto(level.Id, level.Name))
            .ToListAsync(ct);
        var paths = await db.LearningPaths.AsNoTracking()
            .OrderBy(path => path.Name)
            .Select(path => new CourseOptionDto(path.Id, path.Name))
            .ToListAsync(ct);
        return new CreatorCourseOptionsDto(levels, paths);
    }

    public async Task<IReadOnlyList<CreatorCourseDto>> ListForCreatorAsync(
        int creatorUserId,
        CancellationToken ct = default)
    {
        await RequireCreatorAsync(creatorUserId, ct);
        var courses = await CreatorCoursesQuery()
            .Where(course => course.CreatorId == creatorUserId)
            .OrderByDescending(course => course.CreatedAt)
            .ToListAsync(ct);
        var reasons = await LatestReviewReasonsAsync(courses.Select(course => course.Id), ct);
        return courses.Select(course => ToCreatorDto(course, reasons.GetValueOrDefault(course.Id))).ToList();
    }

    public async Task<CreatorCourseDto> GetForCreatorAsync(
        int creatorUserId,
        int courseId,
        CancellationToken ct = default)
    {
        await RequireCreatorAsync(creatorUserId, ct);
        var course = await CreatorCoursesQuery()
            .SingleOrDefaultAsync(item => item.Id == courseId && item.CreatorId == creatorUserId, ct)
            ?? throw new CourseException("COURSE_NOT_FOUND", "Course was not found.", 404);
        var reasons = await LatestReviewReasonsAsync([course.Id], ct);
        return ToCreatorDto(course, reasons.GetValueOrDefault(course.Id));
    }

    public async Task<CreatorCourseDto> CreateForCreatorAsync(
        int creatorUserId,
        CreateCreatorCourseRequest request,
        CancellationToken ct = default)
    {
        var creator = await RequireCreatorAsync(creatorUserId, ct);

        var input = await ValidateCourseInputAsync(
            null, request.Code, request.CourseLevelId, request.LearningPathId, ct);

        var course = new Course
        {
            CreatorId = creatorUserId,
            TrainerId = creatorUserId,
            Status = CourseStatus.Draft,
        };
        ApplyCourseFields(course, input.Code, request.Title, request.Description, request.Category,
            request.CreditCost, request.LearningOutcomes, input.Level, input.Path);
        course.Interests = await ValidateInterestsAsync(request.InterestIds, ct);
        db.Courses.Add(course);
        await db.SaveChangesAsync(ct);

        course.Creator = creator;
        return ToCreatorDto(course, null);
    }

    public async Task<CreatorCourseDto> UpdateForCreatorAsync(
        int creatorUserId,
        int courseId,
        UpdateCreatorCourseRequest request,
        CancellationToken ct = default)
    {
        await RequireCreatorAsync(creatorUserId, ct);
        var course = await CreatorCoursesQuery(tracked: true)
            .SingleOrDefaultAsync(item => item.Id == courseId && item.CreatorId == creatorUserId, ct)
            ?? throw new CourseException("COURSE_NOT_FOUND", "Course was not found.", 404);
        EnsureEditable(course);

        var input = await ValidateCourseInputAsync(
            courseId, request.Code, request.CourseLevelId, request.LearningPathId, ct);
        db.CourseLearningOutcomes.RemoveRange(course.LearningOutcomes);
        var selectedInterestIds = request.InterestIds;
        ApplyCourseFields(course, input.Code, request.Title, request.Description, request.Category,
            request.CreditCost, request.LearningOutcomes, input.Level, input.Path);
        if (selectedInterestIds is not null)
        {
            var validated = await ValidateInterestsAsync(selectedInterestIds, ct);
            var ids = validated.Select(i => i.InterestId).ToHashSet();
            foreach (var removed in course.Interests.Where(i => !ids.Contains(i.InterestId)).ToList())
            {
                course.Interests.Remove(removed);
                db.CourseInterests.Remove(removed);
            }
            foreach (var id in ids.Where(id => course.Interests.All(i => i.InterestId != id)))
                course.Interests.Add(new CourseInterest { CourseId = course.Id, InterestId = id });
        }
        await db.SaveChangesAsync(ct);

        var reasons = await LatestReviewReasonsAsync([course.Id], ct);
        return ToCreatorDto(course, reasons.GetValueOrDefault(course.Id));
    }

    public async Task<CreatorCourseDto> SubmitForCreatorAsync(
        int creatorUserId,
        int courseId,
        CancellationToken ct = default)
    {
        await RequireCreatorAsync(creatorUserId, ct);
        var course = await CreatorCoursesQuery(tracked: true)
            .SingleOrDefaultAsync(item => item.Id == courseId && item.CreatorId == creatorUserId, ct)
            ?? throw new CourseException("COURSE_NOT_FOUND", "Course was not found.", 404);
        EnsureEditable(course);

        if (course.Interests.Count is < 1 or > 4)
            throw new CourseException("COURSE_INTERESTS_REQUIRED", "Choose one to four leaf interests before submitting.");

        course.Status = CourseStatus.PendingApproval;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = creatorUserId,
            Action = "CourseSubmitted",
            EntityType = nameof(Course),
            EntityId = course.Id.ToString(),
            Result = CourseStatus.PendingApproval.ToString(),
        });
        await db.SaveChangesAsync(ct);
        return ToCreatorDto(course, null);
    }

    public async Task<IReadOnlyList<CourseListItemDto>> ListAsync(
        int? userId, string? search, string? category, string? level, bool? featured, CancellationToken ct = default)
    {
        var query = db.Courses.AsNoTracking()
            .Where(c => c.Status == CourseStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = ContainsPattern(search);
            query = query.Where(c =>
                EF.Functions.Like(c.Code, pattern, "\\") ||
                EF.Functions.Like(c.Title, pattern, "\\") ||
                EF.Functions.Like(c.Trainer.FullName, pattern, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);
        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(c => c.Level == level);
        if (featured == true)
            query = query.Where(c => c.IsFeatured);

        var courses = await query.OrderBy(c => c.Code)
            .Select(c => new CourseListRow(
                c.Id,
                c.Code,
                c.Title,
                c.Trainer.FullName,
                c.CreditCost,
                c.Level,
                c.Category,
                c.IsFeatured,
                c.Creator.FullName,
                c.LearningPath == null ? null : c.LearningPath.Name))
            .ToListAsync(ct);
        if (courses.Count == 0) return [];

        var ids = courses.Select(c => c.Id).ToArray();
        var wishlist = userId is null
            ? []
            : await db.WishlistItems.AsNoTracking().Where(w => w.UserId == userId).Select(w => w.CourseId).ToListAsync(ct);
        var wished = wishlist.ToHashSet();
        var interestRows = await db.CourseInterests.AsNoTracking()
            .Where(i => ids.Contains(i.CourseId))
            .Select(i => new { i.CourseId, i.InterestId, i.Interest.Slug, i.Interest.Name, i.Interest.SortOrder })
            .ToListAsync(ct);
        var trainerRows = await db.CourseIntakes.AsNoTracking()
            .Where(i => ids.Contains(i.CourseId)
                && (i.Status == CourseIntakeStatus.Published
                    || i.Status == CourseIntakeStatus.InProgress
                    || i.Status == CourseIntakeStatus.Completed))
            .Select(i => new { i.CourseId, Name = i.Trainer.FullName })
            .ToListAsync(ct);
        var ratingRows = await db.ProgramRatings.AsNoTracking()
            .Where(r => ids.Contains(r.Enrollment.CourseId))
            .GroupBy(r => r.Enrollment.CourseId)
            .Select(g => new { CourseId = g.Key, Count = g.Count(), Average = (double?)g.Average(r => (double)r.Stars) })
            .ToListAsync(ct);

        var tags = interestRows.GroupBy(row => row.CourseId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<CourseInterestDto>)group.OrderBy(row => row.SortOrder)
                .Select(row => new CourseInterestDto(row.InterestId, row.Slug, row.Name)).ToList());
        var trainerNames = trainerRows.GroupBy(row => row.CourseId).ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<string>)group.Select(row => row.Name).Distinct().OrderBy(name => name).ToList());
        var stars = ratingRows.ToDictionary(row => row.CourseId, row => (Count: row.Count, Average: row.Average));

        return courses.Select(c => new CourseListItemDto(
            c.Id, c.Code, c.Title, c.TrainerName, c.CreditCost, c.Level, c.Category, c.IsFeatured,
            wished.Contains(c.Id), tags.GetValueOrDefault(c.Id) ?? [],
            stars.GetValueOrDefault(c.Id).Average, stars.GetValueOrDefault(c.Id).Count,
            CreatorName: c.CreatorName, TrainerNames: trainerNames.GetValueOrDefault(c.Id) ?? [],
            LearningPath: c.LearningPath)).ToList();
    }

    public async Task<CourseDetailDto?> GetByIdAsync(int courseId, int? userId, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking()
            .Include(c => c.Trainer)
            .Include(c => c.Creator)
            .Include(c => c.LearningPath)
            .Include(c => c.Intakes).ThenInclude(i => i.Trainer)
            .Include(c => c.Intakes.Where(i => i.Status == CourseIntakeStatus.Published
                || i.Status == CourseIntakeStatus.InProgress || i.Status == CourseIntakeStatus.Completed
                || i.Status == CourseIntakeStatus.Cancelled)).ThenInclude(i => i.Sessions)
            .Include(c => c.LearningOutcomes)
            .Include(c => c.Interests).ThenInclude(i => i.Interest)
            .FirstOrDefaultAsync(c => c.Id == courseId && c.Status == CourseStatus.Published, ct);
        if (course is null) return null;

        // Count the entire Intake, not just the displayed Session; do not load learner identities.
        var enrollmentCounts = await db.Enrollments.AsNoTracking()
            .Where(e => e.CourseId == courseId
                && (e.Status == EnrollmentStatus.Reserved || e.Status == EnrollmentStatus.Active))
            .GroupBy(e => e.CourseSession.CourseIntakeId)
            .Select(group => new { IntakeId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.IntakeId, item => item.Count, ct);

        var inWishlist = userId is not null &&
            await db.WishlistItems.AnyAsync(w => w.UserId == userId && w.CourseId == courseId, ct);
        var enrolled = userId is not null &&
            await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId
                && (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Reserved), ct);
        var rating = await db.ProgramRatings.AsNoTracking()
            .Where(r => r.Enrollment.CourseId == courseId)
            .GroupBy(r => r.Enrollment.CourseId)
            .Select(g => new { Count = g.Count(), Average = (double?)g.Average(r => (double)r.Stars) })
            .FirstOrDefaultAsync(ct);

        return new CourseDetailDto(
            course.Id,
            course.Code,
            course.Title,
            course.Description,
            course.Trainer.FullName,
            course.CreditCost,
            course.Level,
            course.Category,
            course.Status.ToString(),
            course.LearningOutcomes.OrderBy(o => o.SortOrder).Select(o => o.Text).ToList(),
            course.Intakes.SelectMany(i => i.Sessions).OrderBy(s => s.StartsAt).Select(s => new CourseSessionDto(
                s.Id, s.Label, s.StartsAt, s.EndsAt, s.PhysicalCapacity, s.SeatsLeft,
                s.CourseIntakeId, s.CourseIntake.Status == CourseIntakeStatus.Published ? s.MeetingLink : null,
                s.PhysicalAddress, s.PhysicalCapacity, s.PhysicalBookingDeadline,
                s.CourseIntake.RegistrationOpensAt, s.CourseIntake.RegistrationClosesAt, s.CourseIntake.MinEnrollment,
                s.CourseIntake.Status.ToString(), enrollmentCounts.GetValueOrDefault(s.CourseIntakeId),
                s.CourseIntake.ConfirmedToRunAt, s.CourseIntake.CancelledAt)).ToList(),
            inWishlist,
            enrolled,
            CourseTags(course), rating?.Average, rating?.Count ?? 0,
            course.Intakes.OrderBy(i => i.RegistrationClosesAt).FirstOrDefault()?.MinEnrollment ?? 10,
            course.Intakes.OrderBy(i => i.RegistrationClosesAt).FirstOrDefault()?.RegistrationOpensAt,
            course.Intakes.OrderBy(i => i.RegistrationClosesAt).FirstOrDefault()?.RegistrationClosesAt,
            course.Creator.FullName, PublicTrainerNames(course), course.LearningPath?.Name);
    }

    private static string ContainsPattern(string search)
    {
        var escaped = search.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    private sealed record CourseListRow(
        int Id,
        string Code,
        string Title,
        string TrainerName,
        int CreditCost,
        string Level,
        string Category,
        bool IsFeatured,
        string CreatorName,
        string? LearningPath);

    private static IReadOnlyList<string> PublicTrainerNames(Course course)
        => course.Intakes.Where(i => i.Status is CourseIntakeStatus.Published or CourseIntakeStatus.InProgress or CourseIntakeStatus.Completed)
            .Select(i => i.Trainer.FullName).Distinct().OrderBy(name => name).ToList();

    public async Task<WishlistResultDto> AddToWishlistAsync(int userId, int courseId, CancellationToken ct = default)
    {
        var course = await RequirePublishedCourseAsync(courseId, ct);
        var exists = await db.WishlistItems.AnyAsync(item => item.UserId == userId && item.CourseId == course.Id, ct);
        if (!exists)
        {
            db.WishlistItems.Add(new WishlistItem { UserId = userId, CourseId = course.Id });
            await db.SaveChangesAsync(ct);
        }

        return new WishlistResultDto(course.Id, true);
    }

    public async Task<WishlistResultDto> RemoveFromWishlistAsync(int userId, int courseId, CancellationToken ct = default)
    {
        var course = await RequirePublishedCourseAsync(courseId, ct);
        var item = await db.WishlistItems.FirstOrDefaultAsync(row => row.UserId == userId && row.CourseId == course.Id, ct);
        if (item is not null)
        {
            db.WishlistItems.Remove(item);
            await db.SaveChangesAsync(ct);
        }

        return new WishlistResultDto(course.Id, false);
    }

    private async Task<Course> RequirePublishedCourseAsync(int courseId, CancellationToken ct)
        => await db.Courses.FirstOrDefaultAsync(course => course.Id == courseId && course.Status == CourseStatus.Published, ct)
            ?? throw new KeyNotFoundException("Course not found.");

    private async Task<User> RequireCreatorAsync(int userId, CancellationToken ct)
    {
        var creator = await db.Users.Include(user => user.Roles)
            .SingleOrDefaultAsync(user => user.Id == userId, ct);
        if (creator is null || !creator.IsActive || !creator.Roles.Any(role => role.Role == AppRole.Creator))
            throw new CourseException("CREATOR_REQUIRED", "An active Creator account is required.", 403);
        return creator;
    }

    private IQueryable<Course> CreatorCoursesQuery(bool tracked = false)
    {
        var query = db.Courses
            .Include(course => course.Creator)
            .Include(course => course.CourseLevel)
            .Include(course => course.LearningPath)
            .Include(course => course.LearningOutcomes)
            .Include(course => course.Interests).ThenInclude(i => i.Interest)
            .AsQueryable();
        return tracked ? query : query.AsNoTracking();
    }

    private async Task<Dictionary<int, string?>> LatestReviewReasonsAsync(
        IEnumerable<int> courseIds,
        CancellationToken ct)
    {
        var ids = courseIds.ToArray();
        if (ids.Length == 0) return [];
        var entityIds = ids.Select(id => id.ToString()).ToArray();
        var logs = await db.AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == nameof(Course)
                && log.Action == "CourseRejected"
                && log.EntityId != null
                && entityIds.Contains(log.EntityId))
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync(ct);
        return logs.GroupBy(log => int.Parse(log.EntityId!))
            .ToDictionary(group => group.Key, group => group.First().Reason);
    }

    private static void EnsureEditable(Course course)
    {
        if (course.Status is not (CourseStatus.Draft or CourseStatus.Rejected))
            throw new CourseException(
                "COURSE_NOT_EDITABLE",
                $"A Course in {course.Status} status cannot be edited or submitted.",
                409);
    }

    private async Task<ICollection<CourseInterest>> ValidateInterestsAsync(IReadOnlyList<int>? interestIds, CancellationToken ct)
    {
        var ids = (interestIds ?? []).Distinct().ToArray();
        if (ids.Length > 4 || (interestIds?.Count ?? 0) != ids.Length)
            throw new CourseException("INTEREST_LIMIT", "Choose up to four distinct leaf interests.");
        var selected = await db.Interests.Where(i => ids.Contains(i.Id)).ToListAsync(ct);
        if (selected.Count != ids.Length || selected.Any(i => !i.IsActive))
            throw new CourseException("INTEREST_NOT_FOUND", "Choose interests from the current list.");
        if (selected.Any(i => i.ParentId is null))
            throw new CourseException("INTEREST_NOT_LEAF", "Choose leaf interests, not categories.");
        return ids.Select(id => new CourseInterest { InterestId = id }).ToList();
    }

    private static IReadOnlyList<CourseInterestDto> CourseTags(Course course)
        => course.Interests.OrderBy(i => i.Interest.SortOrder)
            .Select(i => new CourseInterestDto(i.InterestId, i.Interest.Slug, i.Interest.Name)).ToList();

    private async Task<(string Code, CourseLevel Level, LearningPath Path)> ValidateCourseInputAsync(
        int? currentCourseId,
        string requestedCode,
        int courseLevelId,
        int learningPathId,
        CancellationToken ct)
    {
        var code = requestedCode.Trim();
        if (await db.Courses.AnyAsync(course =>
                (!currentCourseId.HasValue || course.Id != currentCourseId.Value)
                && course.Code.ToUpper() == code.ToUpper(), ct))
        {
            throw new CourseException("COURSE_CODE_EXISTS", "Course code is already in use.", 409, "code");
        }

        var level = await db.CourseLevels.FindAsync([courseLevelId], ct)
            ?? throw new CourseException("COURSE_LEVEL_NOT_FOUND", "Select a valid Course level.", 400, "courseLevelId");
        var path = await db.LearningPaths.FindAsync([learningPathId], ct)
            ?? throw new CourseException("LEARNING_PATH_NOT_FOUND", "Select a valid Learning Path.", 400, "learningPathId");
        return (code, level, path);
    }

    private static void ApplyCourseFields(
        Course course,
        string code,
        string title,
        string? description,
        string category,
        int creditCost,
        IReadOnlyList<string>? learningOutcomes,
        CourseLevel level,
        LearningPath path)
    {
        course.Code = code;
        course.Title = title.Trim();
        course.Description = NormalizeOptional(description);
        course.CourseLevelId = level.Id;
        course.CourseLevel = level;
        course.LearningPathId = path.Id;
        course.LearningPath = path;
        course.Level = level.Name;
        course.Category = category.Trim();
        course.CreditCost = creditCost;
        course.LearningOutcomes = NormalizeOutcomes(learningOutcomes)
            .Select((text, index) => new CourseLearningOutcome { SortOrder = index + 1, Text = text })
            .ToList();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> NormalizeOutcomes(IReadOnlyList<string>? outcomes)
        => outcomes?.Where(outcome => !string.IsNullOrWhiteSpace(outcome))
            .Select(outcome => outcome.Trim())
            .ToList() ?? [];

    private static CreatorCourseDto ToCreatorDto(Course course, string? reviewReason)
        => new(
            course.Id,
            course.Code,
            course.Title,
            course.Description,
            course.CreatorId,
            course.Creator.Email,
            course.Creator.FullName,
            course.CourseLevelId ?? 0,
            course.CourseLevel?.Name ?? course.Level,
            course.LearningPathId ?? 0,
            course.LearningPath?.Name ?? "Unassigned",
            course.Category,
            course.CreditCost,
            course.Status.ToString(),
            course.LearningOutcomes.OrderBy(outcome => outcome.SortOrder).Select(outcome => outcome.Text).ToList(),
            course.CreatedAt,
            course.Status == CourseStatus.Rejected ? reviewReason : null,
            course.Interests.Select(i => i.InterestId).ToList());
}
