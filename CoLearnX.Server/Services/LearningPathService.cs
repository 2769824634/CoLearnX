using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CoLearnX.Server.Services;

public sealed class LearningPathService(CoLearnXDbContext db)
{
    private static readonly string[] Names = ["Foundation", "Intermediate", "Advanced"];

    public async Task<string?> GetSuggestedPrerequisiteAsync(int userId, int? subjectId, int? levelId, CancellationToken ct = default)
    {
        if (subjectId is null || levelId is null) return null;
        var pathId = await db.LearningPaths.AsNoTracking().Where(p => p.CourseSubjectId == subjectId)
            .Select(p => (int?)p.Id).SingleOrDefaultAsync(ct);
        var level = await db.CourseLevels.AsNoTracking().Where(l => l.Id == levelId).Select(l => l.SortOrder).SingleOrDefaultAsync(ct);
        if (pathId is null || level <= 1) return null;
        var stages = await db.LearningPathStages.AsNoTracking().Where(s => s.LearningPathId == pathId && s.StageNumber < level).ToListAsync(ct);
        var completions = await db.LearningPathCompletions.AsNoTracking().Where(c => c.UserId == userId && c.LearningPathId == pathId).ToListAsync(ct);
        var awards = await db.UserCertificates.AsNoTracking().Where(c => c.UserId == userId && c.LearningPathId == pathId && c.AdminApproved)
            .Select(c => c.CertificateTemplate.StageNumber).ToListAsync(ct);
        var missing = stages.Where(s => s.StageNumber is >= 1 and <= 3 && !awards.Contains(s.StageNumber)
            && completions.Count(c => c.StageNumber == s.StageNumber) < s.RequiredCourseCount)
            .OrderBy(s => s.StageNumber).Select(s => Names[s.StageNumber - 1]).ToArray();
        return missing.Length == 0 ? null : string.Join(", ", missing);
    }

    public async Task<IReadOnlyList<LearningPathDto>> GetPathsAsync(int? userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var paths = await db.LearningPaths.AsNoTracking().Where(p => p.CourseSubjectId != null)
            .OrderBy(p => p.CourseSubject!.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);
        var stages = await db.LearningPathStages.AsNoTracking().ToListAsync(ct);
        var courses = await db.Courses.AsNoTracking()
            .Where(c => c.Status == CourseStatus.Published && c.CourseSubjectId != null && c.CourseLevelId != null)
            .Select(c => new { c.Id, c.Code, c.Title, c.CreditCost, SubjectId = c.CourseSubjectId!.Value,
                Level = c.CourseLevel!.SortOrder,
                HasClass = c.Intakes.Any(i => i.Status == CourseIntakeStatus.Published && i.Sessions.Any(s => s.StartsAt > now)) })
            .ToListAsync(ct);
        var completions = userId is null ? [] : await db.LearningPathCompletions.AsNoTracking()
            .Where(c => c.UserId == userId).ToListAsync(ct);
        var awarded = userId is null ? [] : await db.UserCertificates.AsNoTracking()
            .Where(c => c.UserId == userId && c.AdminApproved && c.LearningPathId != null)
            .Select(c => new { PathId = c.LearningPathId!.Value, Stage = c.CertificateTemplate.StageNumber }).ToListAsync(ct);
        var enrollmentIds = userId is null ? [] : await db.Enrollments.AsNoTracking()
            .Where(e => e.UserId == userId && (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Reserved || e.Status == EnrollmentStatus.Completed))
            .Select(e => e.CourseId).ToListAsync(ct);
        var enrolled = enrollmentIds.ToHashSet();

        return paths.Select(path =>
        {
            var result = new List<LearningPathStageDto>();
            for (var number = 1; number <= 3; number++)
            {
                var count = completions.Count(c => c.LearningPathId == path.Id && c.StageNumber == number);
                var required = stages.FirstOrDefault(s => s.LearningPathId == path.Id && s.StageNumber == number)?.RequiredCourseCount ?? 1;
                var finished = count >= required || awarded.Any(c => c.PathId == path.Id && c.Stage == number);
                var missing = result.Where(s => !s.Completed).Select(s => s.Name).ToArray();
                var stageCourses = courses.Where(c => c.SubjectId == path.CourseSubjectId && c.Level == number)
                    .OrderBy(c => c.Title).Select(c => new LearningPathCourseDto(c.Id, c.Code, c.Title, c.CreditCost, c.HasClass,
                        completions.Any(done => done.CourseId == c.Id && done.LearningPathId == path.Id && done.StageNumber == number), enrolled.Contains(c.Id))).ToList();
                result.Add(new LearningPathStageDto(number, Names[number - 1], required, count, finished,
                    missing.Length == 0 ? null : string.Join(", ", missing), stageCourses));
            }
            return new LearningPathDto(path.Id, path.CourseSubjectId!.Value, path.Name, result,
                awarded.Any(c => c.PathId == path.Id && c.Stage == 4));
        }).ToList();
    }

    // Record each certified course once. The snapshot preserves earned progress
    // if a course is later unpublished or its subject is edited.
    public Task ReconcileUserAsync(int userId, CancellationToken ct = default)
        => db.Database.CurrentTransaction is not null
            ? ReconcileUserCoreAsync(userId, ct)
            : InTransactionAsync(async () => { await ReconcileUserCoreAsync(userId, ct); return true; }, ct);

    private async Task ReconcileUserCoreAsync(int userId, CancellationToken ct)
    {
        var certificates = await (from certificate in db.UserCertificates.AsNoTracking()
            join course in db.Courses.AsNoTracking() on certificate.CourseId equals course.Id
            join path in db.LearningPaths.AsNoTracking() on course.CourseSubjectId equals path.CourseSubjectId
            where certificate.UserId == userId && certificate.AdminApproved && certificate.LearningPathId == null
                && course.CourseSubjectId != null && course.CourseLevelId != null
                && db.Enrollments.Any(e => e.UserId == userId && e.CourseId == course.Id && e.Status == EnrollmentStatus.Completed)
            select new { certificate.Id, certificate.AwardedAt, CourseId = course.Id, PathId = path.Id, Stage = course.CourseLevel!.SortOrder })
            .OrderBy(x => x.AwardedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var completed = await db.LearningPathCompletions.Where(c => c.UserId == userId).ToListAsync(ct);
        var completedCourses = completed.Select(c => c.CourseId).ToHashSet();
        foreach (var certificate in certificates.Where(c => c.Stage is >= 1 and <= 3))
        {
            if (!completedCourses.Add(certificate.CourseId)) continue;
            var completion = new LearningPathCompletion { UserId = userId, CourseId = certificate.CourseId,
                LearningPathId = certificate.PathId, StageNumber = certificate.Stage, CertificateId = certificate.Id, CompletedAt = certificate.AwardedAt };
            db.LearningPathCompletions.Add(completion);
            completed.Add(completion);
        }

        var pathIds = completed.Select(c => c.LearningPathId).Distinct().ToArray();
        var stages = await db.LearningPathStages.Where(s => pathIds.Contains(s.LearningPathId)).ToListAsync(ct);
        var templates = await db.CertificateTemplates.ToDictionaryAsync(t => t.StageNumber, ct);
        var awarded = await db.UserCertificates.Where(c => c.UserId == userId && c.LearningPathId != null)
            .Select(c => new { PathId = c.LearningPathId!.Value, c.CertificateTemplateId }).ToListAsync(ct);
        var awardedKeys = awarded.Select(c => (c.PathId, c.CertificateTemplateId)).ToHashSet();
        foreach (var pathId in pathIds)
        {
            var stageComplete = new bool[3];
            foreach (var stage in stages.Where(s => s.LearningPathId == pathId && s.StageNumber is >= 1 and <= 3))
            {
                if (!templates.TryGetValue(stage.StageNumber, out var template)) continue;
                var key = (pathId, template.Id);
                var eligible = completed.Count(c => c.LearningPathId == pathId && c.StageNumber == stage.StageNumber) >= stage.RequiredCourseCount;
                stageComplete[stage.StageNumber - 1] = eligible || awardedKeys.Contains(key);
                if (eligible && awardedKeys.Add(key)) db.UserCertificates.Add(NewAward(userId, pathId, template.Id));
            }
            if (stageComplete.All(x => x) && templates.TryGetValue(4, out var graduation) && awardedKeys.Add((pathId, graduation.Id)))
                db.UserCertificates.Add(NewAward(userId, pathId, graduation.Id));
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ReconcileAllAsync(CancellationToken ct = default)
    {
        var users = await db.UserCertificates.AsNoTracking().Where(c => c.AdminApproved && c.CourseId != null && c.LearningPathId == null)
            .Select(c => c.UserId).Distinct().ToListAsync(ct);
        foreach (var user in users) await ReconcileUserAsync(user, ct);
    }

    public Task<LearningPathDto> UpdateStageAsync(int adminId, int pathId, int number, int required, CancellationToken ct = default)
        => InTransactionAsync(() => UpdateStageCoreAsync(adminId, pathId, number, required, ct), ct);

    private async Task<LearningPathDto> UpdateStageCoreAsync(int adminId, int pathId, int number, int required, CancellationToken ct)
    {
        if (required is < 1 or > 100)
            throw new LaterPhaseException("PATH_REQUIREMENT_INVALID", "Choose between 1 and 100 courses.", 400, "requiredCourseCount");
        var stage = await db.LearningPathStages.SingleOrDefaultAsync(s => s.LearningPathId == pathId && s.StageNumber == number, ct)
            ?? throw new LaterPhaseException("PATH_STAGE_NOT_FOUND", "The learning path stage was not found.", 404);
        stage.RequiredCourseCount = required;
        db.AuditLogs.Add(new AuditLog { AdminAccountId = adminId, Action = "LearningPathRequirementUpdated",
            EntityType = nameof(LearningPath), EntityId = pathId.ToString(), Result = "Succeeded",
            Reason = $"Stage {number}: {required} courses" });
        await db.SaveChangesAsync(ct);
        var users = await db.LearningPathCompletions.AsNoTracking().Where(c => c.LearningPathId == pathId)
            .Select(c => c.UserId).Distinct().ToListAsync(ct);
        foreach (var user in users) await ReconcileUserAsync(user, ct);
        return (await GetPathsAsync(null, ct)).Single(path => path.Id == pathId);
    }

    private Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                    var result = await action();
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch (Exception error) when (attempt < 4 && IsRetryableConflict(error))
                {
                    db.ChangeTracker.Clear();
                    await Task.Delay(25 * (attempt + 1), ct);
                }
            }
        });

    private static bool IsRetryableConflict(Exception error)
        => error is SqliteException { SqliteErrorCode: 5 or 6 }
            or SqliteException { SqliteExtendedErrorCode: 2067 }
            or SqlException { Number: 1205 or 2601 or 2627 }
            || error.InnerException is not null && IsRetryableConflict(error.InnerException);

    private static UserCertificate NewAward(int userId, int pathId, int templateId) => new()
    {
        UserId = userId, LearningPathId = pathId, CertificateTemplateId = templateId,
        VerificationCode = Guid.NewGuid().ToString("N").ToUpperInvariant(), AdminApproved = true,
    };
}
