using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed class RecommendationService(CoLearnXDbContext db)
{
    public async Task<IReadOnlyList<InterestDto>> InterestTreeAsync(CancellationToken ct)
    {
        var interests = await db.Interests.AsNoTracking().Where(i => i.IsActive && i.Slug != "")
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Name).ToListAsync(ct);
        return interests.Where(i => i.ParentId is null)
            .Select(parent => new InterestDto(parent.Id, parent.Slug, parent.Name,
                interests.Where(i => i.ParentId == parent.Id)
                    .Select(i => new InterestDto(i.Id, i.Slug, i.Name, [])).ToList()))
            .ToList();
    }

    public async Task<MemberInterestsDto> GetMemberInterestsAsync(int userId, CancellationToken ct)
    {
        var preference = await db.UserPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new CourseException("USER_NOT_FOUND", "Member account was not found.", 404);
        var ids = await db.UserInterests.AsNoTracking().Where(i => i.UserId == userId)
            .Select(i => i.InterestId).ToListAsync(ct);
        return new MemberInterestsDto(ids, preference.LearningGoals, preference.OnboardingCompletedAt, preference.OnboardingSkippedAt);
    }

    public async Task<MemberInterestsDto> SaveMemberInterestsAsync(int userId, UpdateMemberInterestsRequest request, CancellationToken ct)
    {
        var preference = await db.UserPreferences.SingleOrDefaultAsync(p => p.UserId == userId, ct)
            ?? throw new CourseException("USER_NOT_FOUND", "Member account was not found.", 404);
        var ids = request.Skip ? [] : (request.InterestIds ?? []).Distinct().ToArray();
        if (ids.Length > 8)
            throw new CourseException("INTEREST_LIMIT", "Choose up to eight interests.");
        if (!request.Skip)
        {
            var selected = await db.Interests.Where(i => ids.Contains(i.Id)).ToListAsync(ct);
            if (selected.Count != ids.Length || selected.Any(i => !i.IsActive))
                throw new CourseException("INTEREST_NOT_FOUND", "Choose interests from the current list.");
            if (selected.Any(i => i.ParentId is null))
                throw new CourseException("INTEREST_NOT_LEAF", "Choose a leaf interest, not a category.");
            if (request.LearningGoals is not ("hobby" or "professional"))
                throw new CourseException("LEARNING_GOAL_INVALID", "Choose hobby or professional.");
        }
        var current = await db.UserInterests.Where(i => i.UserId == userId).ToListAsync(ct);
        db.UserInterests.RemoveRange(current.Where(i => !ids.Contains(i.InterestId)));
        db.UserInterests.AddRange(ids.Where(id => current.All(i => i.InterestId != id))
            .Select(id => new UserInterest { UserId = userId, InterestId = id }));
        preference.LearningGoals = request.Skip ? null : request.LearningGoals;
        preference.OnboardingCompletedAt = request.Skip ? null : DateTime.UtcNow;
        preference.OnboardingSkippedAt = request.Skip ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return await GetMemberInterestsAsync(userId, ct);
    }

    public async Task<ProgramRatingDto> RateAsync(int userId, int enrollmentId, SubmitRatingRequest request, CancellationToken ct)
    {
        if (request.Stars is < 1 or > 5 || request.Comment?.Length > 500)
            throw new CourseException("RATING_INVALID", "Give one to five stars and a comment of at most 500 characters.");
        var enrollment = await db.Enrollments.SingleOrDefaultAsync(e => e.Id == enrollmentId && e.UserId == userId, ct)
            ?? throw new CourseException("ENROLLMENT_NOT_FOUND", "Enrollment was not found.", 404);
        if (enrollment.Status != EnrollmentStatus.Completed || enrollment.ProgressPercent < 100)
            throw new CourseException("RATING_NOT_COMPLETED", "Complete the course before rating it.");
        var existing = await db.ProgramRatings.Include(r => r.Enrollment)
            .Where(r => r.Enrollment.UserId == userId && r.Enrollment.CourseId == enrollment.CourseId)
            .OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            existing = new ProgramRating { EnrollmentId = enrollment.Id, Stars = request.Stars,
                Comment = request.Comment?.Trim() };
            db.ProgramRatings.Add(existing);
        }
        else
        {
            existing.Stars = request.Stars;
            existing.Comment = request.Comment?.Trim();
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return new ProgramRatingDto(existing.EnrollmentId, enrollment.CourseId, existing.Stars,
            existing.Comment, existing.CreatedAt, existing.UpdatedAt);
    }

    public async Task<RecommendationResponseDto> RecommendAsync(int userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var completions = await db.LearningPathCompletions.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CompletedAt).ToListAsync(ct);
        var paths = await db.LearningPaths.AsNoTracking().Where(path => path.CourseSubjectId != null).ToListAsync(ct);
        var pathBySubject = paths.ToDictionary(path => path.CourseSubjectId!.Value);
        var highestByPath = completions.GroupBy(item => item.LearningPathId)
            .ToDictionary(group => group.Key, group => group.Max(item => item.StageNumber));
        var excluded = (await db.Enrollments.AsNoTracking().Where(item => item.UserId == userId)
            .Select(item => item.CourseId).ToListAsync(ct)).ToHashSet();
        var anchor = completions.FirstOrDefault();
        var target = anchor is null
            ? (await db.UserInterests.AsNoTracking().Where(item => item.UserId == userId)
                .Select(item => item.InterestId).ToListAsync(ct)).ToHashSet()
            : (await db.CourseInterests.AsNoTracking().Where(item => item.CourseId == anchor.CourseId)
                .Select(item => item.InterestId).ToListAsync(ct)).ToHashSet();
        var selectedInterests = await db.Interests.AsNoTracking().Include(item => item.Parent)
            .Where(item => target.Contains(item.Id)).ToListAsync(ct);
        var areas = selectedInterests.Select(item => InterestSubjectMap.For(item.Parent?.Slug, item.Slug))
            .Where(area => area != null).Select(area => area!.Value).ToHashSet();
        var candidates = await db.Courses.AsNoTracking().AsSplitQuery()
            .Include(course => course.CourseLevel).Include(course => course.CourseSubject)
            .Include(course => course.Interests).ThenInclude(item => item.Interest)
            .Include(course => course.Intakes).ThenInclude(intake => intake.Sessions)
            .Where(course => course.Status == CourseStatus.Published && course.CourseLevelId != null && course.CourseSubjectId != null)
            .ToListAsync(ct);
        var eligible = candidates.Where(course =>
        {
            if (excluded.Contains(course.Id) || !pathBySubject.TryGetValue(course.CourseSubjectId!.Value, out var path)) return false;
            if (anchor is not null)
                return highestByPath.TryGetValue(path.Id, out var highest) && course.CourseLevel!.SortOrder >= highest;
            return course.CourseLevel!.SortOrder == 1 &&
                (target.Count == 0 || course.Interests.Any(item => target.Contains(item.InterestId)) || areas.Contains(course.CourseSubject!.Subject));
        }).ToList();
        var courseIds = eligible.Select(course => course.Id).ToArray();
        var ratings = await db.ProgramRatings.AsNoTracking().Where(item => courseIds.Contains(item.Enrollment.CourseId))
            .Select(item => new { item.Enrollment.CourseId, item.Stars }).ToListAsync(ct);
        var byCourse = ratings.GroupBy(item => item.CourseId)
            .ToDictionary(group => group.Key, group => new { Count = group.Count(), Average = group.Average(item => item.Stars) });
        var stageRules = await db.LearningPathStages.AsNoTracking().ToListAsync(ct);
        var awards = await db.UserCertificates.AsNoTracking()
            .Where(item => item.UserId == userId && item.AdminApproved && item.LearningPathId != null)
            .Select(item => new { PathId = item.LearningPathId!.Value, Stage = item.CertificateTemplate.StageNumber }).ToListAsync(ct);
        var stageNames = new[] { "Foundation", "Intermediate", "Advanced" };
        var items = eligible.Select(course =>
        {
            var path = pathBySubject[course.CourseSubjectId!.Value];
            var level = course.CourseLevel!.SortOrder;
            var highest = highestByPath.GetValueOrDefault(path.Id);
            var rank = anchor is null ? 0 : level == Math.Min(highest + 1, 3) ? 0 : level == highest ? 1 : 2;
            var missing = Enumerable.Range(1, Math.Clamp(level - 1, 0, 3)).Where(number =>
            {
                var rule = stageRules.FirstOrDefault(item => item.LearningPathId == path.Id && item.StageNumber == number);
                return completions.Count(item => item.LearningPathId == path.Id && item.StageNumber == number) < (rule?.RequiredCourseCount ?? 1)
                    && !awards.Any(item => item.PathId == path.Id && item.Stage == number);
            }).Select(number => stageNames[number - 1]).ToArray();
            var openIntakes = course.Intakes.Where(intake => intake.Status == CourseIntakeStatus.Published
                && intake.RegistrationOpensAt <= now && intake.RegistrationClosesAt > now
                && intake.Sessions.Any(session => session.StartsAt > now && (session.PhysicalCapacity == 0 || session.SeatsTaken < session.PhysicalCapacity))).ToList();
            var open = openIntakes.Count > 0;
            var nextClass = openIntakes.SelectMany(intake => intake.Sessions).Where(session => session.StartsAt > now)
                .Select(session => (DateTime?)session.StartsAt).Min();
            var tags = course.Interests.Select(item => item.InterestId).ToHashSet();
            var union = target.Union(tags).Count();
            var similarity = union == 0 ? 0d : (double)target.Intersect(tags).Count() / union;
            byCourse.TryGetValue(course.Id, out var rating);
            var count = rating?.Count ?? 0;
            var average = rating?.Average;
            var bayes = (count * (average ?? 3d) + 5 * 3d) / (count + 5);
            var score = 0.55 * bayes / 5 + 0.30 * similarity + 0.15;
            return new
            {
                Rank = rank, Missing = missing.Length, Open = open, NextClass = nextClass, Advanced = level > highest,
                Item = new RecommendationItemDto(course.Id, course.Code, course.Title, course.CourseLevel.Name,
                    course.CreditCost, course.Interests.Select(item => new CourseInterestDto(item.InterestId,
                        item.Interest.Slug, item.Interest.Name)).ToList(), average, count, score,
                    path.Name, missing.Length == 0 ? null : string.Join(", ", missing), open)
            };
        }).OrderBy(item => item.Rank).ThenBy(item => item.Missing).ThenByDescending(item => item.Open)
          .ThenByDescending(item => item.Item.Score).ThenBy(item => item.NextClass).ThenBy(item => item.Item.CourseId)
          .ToList();
        var mode = anchor is null ? "coldStart" : items.Any(item => item.Advanced) ? "nextLevel" : "mastery";
        return new RecommendationResponseDto(mode, items.Take(8).Select(item => item.Item).ToList(), anchor?.CourseId);
    }

}
