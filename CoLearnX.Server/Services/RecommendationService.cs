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
        var enrollments = await db.Enrollments.AsNoTracking()
            .Include(e => e.CourseSession)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CompletedAt ?? e.EnrolledAt)
            .ToListAsync(ct);
        var excluded = enrollments.Where(e => e.Status is EnrollmentStatus.Active or EnrollmentStatus.Completed or EnrollmentStatus.Reserved)
            .Select(e => e.CourseId).ToHashSet();
        var anchor = enrollments.FirstOrDefault(e => e.Status == EnrollmentStatus.Completed);
        var target = anchor is null
            ? (await db.UserInterests.AsNoTracking().Where(i => i.UserId == userId)
                .Select(i => i.InterestId).ToListAsync(ct)).ToHashSet()
            : (await db.CourseInterests.AsNoTracking().Where(i => i.CourseId == anchor.CourseId)
                .Select(i => i.InterestId).ToListAsync(ct)).ToHashSet();
        if (anchor is not null && target.Count == 0)
            return new RecommendationResponseDto("sameLevel", [], anchor.CourseId);
        var mode = "coldStart";
        var desiredOrder = 1;
        if (anchor is not null)
        {
            var level = await db.Courses.Where(c => c.Id == anchor.CourseId)
                .Select(c => c.CourseLevel!.SortOrder).SingleAsync(ct);
            var assessmentIds = await db.Assessments.Where(a => a.CourseIntakeId == anchor.CourseSession.CourseIntakeId)
                .Select(a => new { a.Id, a.PassScore }).ToListAsync(ct);
            var results = await db.AssessmentResults.Where(r => r.EnrollmentId == anchor.Id)
                .Select(r => new { r.AssessmentId, r.Score }).ToListAsync(ct);
            var qualified = anchor.ProgressPercent == 100 && assessmentIds.All(a =>
                results.Any(r => r.AssessmentId == a.Id && r.Score >= a.PassScore));
            desiredOrder = qualified ? Math.Min(level + 1, 3) : level;
            mode = qualified ? (level >= 3 ? "mastery" : "nextLevel") : "sameLevel";
        }
        var courses = await db.Courses.AsNoTracking().AsSplitQuery()
            .Include(c => c.CourseLevel).Include(c => c.Interests).ThenInclude(ci => ci.Interest)
            .Include(c => c.Intakes).ThenInclude(i => i.Sessions)
            .Where(c => c.Status == CourseStatus.Published && c.CourseLevel != null
                && (c.CourseLevel.SortOrder == desiredOrder ||
                    (mode == "nextLevel" && c.CourseLevel.SortOrder == desiredOrder - 1)))
            .ToListAsync(ct);
        bool IsEligible(Course c) => !excluded.Contains(c.Id) && c.Interests.Count is >= 1 and <= 4
            && (target.Count == 0 || c.Interests.Any(ci => target.Contains(ci.InterestId)))
            && c.Intakes.Any(i => i.Status == CourseIntakeStatus.Published
                && i.RegistrationOpensAt <= now && i.RegistrationClosesAt > now
                && i.Sessions.Any(s => s.PhysicalCapacity == 0 || (s.SeatsTaken < s.PhysicalCapacity
                    && (s.PhysicalBookingDeadline == null || s.PhysicalBookingDeadline >= now))));
        var eligible = courses.Where(c => c.CourseLevel!.SortOrder == desiredOrder && IsEligible(c)).ToList();
        if (mode == "nextLevel" && eligible.Count == 0)
        {
            mode = "mastery";
            eligible = courses.Where(c => c.CourseLevel!.SortOrder == desiredOrder - 1 && IsEligible(c)).ToList();
        }
        var courseIds = eligible.Select(c => c.Id).ToArray();
        var ratings = await db.ProgramRatings.AsNoTracking()
            .Where(r => courseIds.Contains(r.Enrollment.CourseId))
            .Select(r => new { r.Enrollment.CourseId, r.Stars }).ToListAsync(ct);
        var byCourse = ratings.GroupBy(r => r.CourseId)
            .ToDictionary(g => g.Key, g => new { Count = g.Count(), Average = g.Average(r => r.Stars) });
        var items = eligible.Select(c =>
        {
            var tags = c.Interests.Select(i => i.InterestId).ToHashSet();
            var overlap = target.Intersect(tags).Count();
            var union = target.Union(tags).Count();
            var jaccard = union == 0 ? 0d : (double)overlap / union;
            byCourse.TryGetValue(c.Id, out var rating);
            var count = rating?.Count ?? 0;
            var average = rating?.Average;
            var bayes = (count * (average ?? 3d) + 5 * 3d) / (count + 5);
            var score = 0.55 * bayes / 5 + 0.30 * jaccard + 0.15;
            return new RecommendationItemDto(c.Id, c.Code, c.Title, c.CourseLevel!.Name,
                c.CreditCost, c.Interests.Select(i => new CourseInterestDto(i.InterestId,
                    i.Interest.Slug, i.Interest.Name)).ToList(), average, count, score);
        }).OrderByDescending(i => i.Score).ThenBy(i => i.CourseId).Take(8).ToList();
        return new RecommendationResponseDto(mode, items, anchor?.CourseId);
    }
}
