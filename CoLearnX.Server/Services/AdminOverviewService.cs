using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IAdminOverviewService
{
    Task<IReadOnlyList<AdminClassRowDto>> ListClassesAsync(CancellationToken ct = default);
    Task<AdminClassDetailDto?> GetClassAsync(int classId, CancellationToken ct = default);
    Task<AdminUserActivityDto?> GetUserActivityAsync(int userId, CancellationToken ct = default);
}

// Read-only views for administrators: every class at a glance, and one person's teaching,
// course ownership or learning. All numbers come from existing enrolment and attendance rows.
public sealed class AdminOverviewService(CoLearnXDbContext db, TimeProvider? clock = null) : IAdminOverviewService
{
    private static readonly EnrollmentStatus[] LearnerStatuses =
        [EnrollmentStatus.Active, EnrollmentStatus.Reserved, EnrollmentStatus.Completed];

    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<AdminClassRowDto>> ListClassesAsync(CancellationToken ct = default)
        => await BuildRowsAsync(db.CourseIntakes, ct);

    public async Task<AdminClassDetailDto?> GetClassAsync(int classId, CancellationToken ct = default)
    {
        var row = (await BuildRowsAsync(db.CourseIntakes.Where(i => i.Id == classId), ct)).SingleOrDefault();
        if (row is null) return null;

        var now = Now;
        var sessions = await db.CourseSessions.AsNoTracking().Where(s => s.CourseIntakeId == classId)
            .OrderBy(s => s.StartsAt).ToListAsync(ct);
        var heldIds = sessions.Where(s => s.StartsAt <= now).Select(s => s.Id).ToHashSet();
        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(e => e.CourseSession.CourseIntakeId == classId)
            .Select(e => new { e.Id, e.UserId, e.User.FullName, e.User.Email, e.Status })
            .ToListAsync(ct);
        var learners = enrollments.Where(e => LearnerStatuses.Contains(e.Status)).ToList();
        var attended = await AttendedAsync(sessions.Select(s => s.Id).ToList(), ct);
        var certificates = await db.CertificateRequests.AsNoTracking()
            .Where(c => c.Enrollment.CourseSession.CourseIntakeId == classId)
            .OrderByDescending(c => c.SubmittedAt)
            .Select(c => new { c.EnrollmentId, c.Status })
            .ToListAsync(ct);

        var sessionRows = sessions.Select(s => new AdminClassSessionDto(s.Id, s.Label, s.StartsAt, s.EndsAt,
            heldIds.Contains(s.Id),
            attended.Count(a => a.SessionId == s.Id && learners.Any(l => l.UserId == a.UserId)),
            learners.Count)).ToList();

        var learnerRows = enrollments.OrderBy(e => e.FullName).Select(e =>
        {
            var count = attended.Count(a => a.UserId == e.UserId && heldIds.Contains(a.SessionId));
            var certificate = certificates.FirstOrDefault(c => c.EnrollmentId == e.Id);
            return new AdminClassLearnerDto(e.UserId, e.FullName, e.Email, e.Status.ToString(), count, heldIds.Count,
                Rate(count, heldIds.Count), certificate?.Status.ToString());
        }).ToList();

        var intakeMaterials = await db.CourseIntakeMaterials.AsNoTracking()
            .Where(m => m.CourseIntakeId == classId)
            .Select(m => m.CourseMaterialVersion.LearningMaterial.Title)
            .ToListAsync(ct);
        var sessionMaterials = await db.SessionMaterials.AsNoTracking()
            .Where(m => m.CourseSession.CourseIntakeId == classId)
            .Select(m => new { m.Title, m.CourseSession.Label })
            .ToListAsync(ct);
        var materials = intakeMaterials.Select(title => new AdminClassMaterialDto(title, "Whole class"))
            .Concat(sessionMaterials.Select(m => new AdminClassMaterialDto(m.Title, m.Label)))
            .ToList();

        return new AdminClassDetailDto(row, sessionRows, learnerRows, materials);
    }

    public async Task<AdminUserActivityDto?> GetUserActivityAsync(int userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.Roles)
            .Include(u => u.Specialties).ThenInclude(s => s.CourseSubject)
            .SingleOrDefaultAsync(u => u.Id == userId && u.Email != PlatformIdentity.Email
                && !EF.Functions.Like(u.Email, "%@staff.colearnx"), ct);
        if (user is null) return null;

        var roles = user.Roles.Where(r => r.IsVisible).OrderBy(r => r.Role).ToList();
        var certified = roles.Where(r => r.Role is AppRole.Trainer or AppRole.Creator).All(r => r.CertifiedAt != null);

        var teaching = await BuildRowsAsync(db.CourseIntakes.Where(i => i.TrainerId == userId), ct);

        var courses = await db.Courses.AsNoTracking().Where(c => c.CreatorId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.Id, c.Code, c.Title, c.Status })
            .ToListAsync(ct);
        var courseIds = courses.Select(c => c.Id).ToList();
        var courseClasses = await BuildRowsAsync(db.CourseIntakes.Where(i => courseIds.Contains(i.CourseId)), ct);
        var created = courses.Select(c =>
        {
            var classes = courseClasses.Where(r => r.CourseId == c.Id).ToList();
            return new AdminCreatorCourseDto(c.Id, c.Code, c.Title, c.Status.ToString(), classes.Count,
                classes.Sum(r => r.Learners), classes);
        }).ToList();

        var ledger = await db.CreditTransactions.AsNoTracking().Where(t => t.UserId == userId)
            .Select(t => new { t.Type, t.Delta }).ToListAsync(ct);

        var now = Now;
        var enrollments = await db.Enrollments.AsNoTracking().Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new
            {
                e.Id, e.CourseId, e.Course.Code, e.Course.Title, e.Status, e.CreditsSpent,
                ClassId = (int?)e.CourseSession.CourseIntakeId,
                StartsAt = (DateTime?)e.CourseSession.CourseIntake.StartsAt,
                EndsAt = (DateTime?)e.CourseSession.CourseIntake.EndsAt,
            })
            .ToListAsync(ct);
        var classIds = enrollments.Where(e => e.ClassId != null).Select(e => e.ClassId!.Value).Distinct().ToList();
        var heldSessions = await db.CourseSessions.AsNoTracking()
            .Where(s => classIds.Contains(s.CourseIntakeId) && s.StartsAt <= now)
            .Select(s => new { s.Id, s.CourseIntakeId })
            .ToListAsync(ct);
        var heldIds = heldSessions.Select(s => s.Id).ToList();
        var attendedSessionIds = await db.AttendanceRecords.AsNoTracking()
            .Where(a => a.UserId == userId && heldIds.Contains(a.CourseSessionId)
                && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late))
            .Select(a => a.CourseSessionId)
            .Distinct()
            .ToListAsync(ct);
        var learning = enrollments.Select(e =>
        {
            var held = heldSessions.Where(s => s.CourseIntakeId == e.ClassId).Select(s => s.Id).ToList();
            var count = held.Count(attendedSessionIds.Contains);
            return new AdminMemberCourseDto(e.Id, e.CourseId, e.Code, e.Title, e.ClassId, e.StartsAt, e.EndsAt,
                e.Status.ToString(), e.CreditsSpent, count, held.Count, Rate(count, held.Count));
        }).ToList();

        var certificates = await db.UserCertificates.AsNoTracking().Where(c => c.UserId == userId)
            .OrderByDescending(c => c.AwardedAt)
            .Select(c => new
            {
                c.Id, c.CertificateTemplate.Title, c.AwardedAt,
                CourseCode = db.Courses.Where(course => course.Id == c.CourseId).Select(course => course.Code).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return new AdminUserActivityDto(
            user.Id,
            roles.Select(r => r.Role.ToString()).ToList(),
            certified,
            user.Specialties.Select(s => s.CourseSubject.Name).OrderBy(name => name).ToList(),
            teaching,
            created,
            ledger.Where(t => t.Type == CreditTransactionType.Royalty).Sum(t => t.Delta),
            learning,
            certificates.Select(c => new AdminMemberCertificateDto(c.Id, c.Title, c.CourseCode, c.AwardedAt)).ToList(),
            ledger.Where(t => t.Type == CreditTransactionType.TopUp).Sum(t => t.Delta),
            enrollments.Where(e => e.Status is EnrollmentStatus.Active or EnrollmentStatus.Completed).Sum(e => e.CreditsSpent));
    }

    private async Task<IReadOnlyList<AdminClassRowDto>> BuildRowsAsync(IQueryable<CourseIntake> source, CancellationToken ct)
    {
        var intakes = await source.AsNoTracking()
            .OrderByDescending(i => i.StartsAt).ThenByDescending(i => i.Id)
            .Select(i => new
            {
                i.Id, i.CourseId, i.Course.Code, i.Course.Title,
                i.Course.CreatorId, CreatorName = i.Course.Creator.FullName,
                i.TrainerId, TrainerName = i.Trainer.FullName,
                i.StartsAt, i.EndsAt, i.Status, i.MinEnrollment,
            })
            .ToListAsync(ct);
        if (intakes.Count == 0) return [];

        var ids = intakes.Select(i => i.Id).ToList();
        var now = Now;
        var sessions = await db.CourseSessions.AsNoTracking().Where(s => ids.Contains(s.CourseIntakeId))
            .Select(s => new { s.Id, s.CourseIntakeId, s.StartsAt })
            .ToListAsync(ct);
        var learners = await db.Enrollments.AsNoTracking()
            .Where(e => ids.Contains(e.CourseSession.CourseIntakeId) && LearnerStatuses.Contains(e.Status))
            .Select(e => new { e.UserId, e.CourseSession.CourseIntakeId })
            .ToListAsync(ct);
        var attended = await AttendedAsync(sessions.Where(s => s.StartsAt <= now).Select(s => s.Id).ToList(), ct);

        return intakes.Select(i =>
        {
            var classSessions = sessions.Where(s => s.CourseIntakeId == i.Id).ToList();
            var held = classSessions.Where(s => s.StartsAt <= now).Select(s => s.Id).ToHashSet();
            var learnerIds = learners.Where(l => l.CourseIntakeId == i.Id).Select(l => l.UserId).ToHashSet();
            var present = attended.Count(a => held.Contains(a.SessionId) && learnerIds.Contains(a.UserId));
            return new AdminClassRowDto(i.Id, i.CourseId, i.Code, i.Title, i.CreatorId, i.CreatorName,
                i.TrainerId, i.TrainerName, i.StartsAt, i.EndsAt, i.Status.ToString(), learnerIds.Count,
                i.MinEnrollment, classSessions.Count, held.Count, Rate(present, held.Count * learnerIds.Count));
        }).ToList();
    }

    // One row per learner and session they attended; Late still counts as attended.
    private async Task<List<(int SessionId, int UserId)>> AttendedAsync(List<int> sessionIds, CancellationToken ct)
    {
        if (sessionIds.Count == 0) return [];
        var rows = await db.AttendanceRecords.AsNoTracking()
            .Where(a => sessionIds.Contains(a.CourseSessionId)
                && (a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late))
            .Select(a => new { a.CourseSessionId, a.UserId })
            .Distinct()
            .ToListAsync(ct);
        return rows.Select(a => (a.CourseSessionId, a.UserId)).ToList();
    }

    private static double? Rate(int attended, int possible)
        => possible <= 0 ? null : Math.Round((double)attended / possible, 4);
}
