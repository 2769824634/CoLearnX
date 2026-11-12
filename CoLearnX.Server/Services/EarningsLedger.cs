using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed class EarningsOptions
{
    public int TrainerPercent { get; set; } = EarningsRules.DefaultDeliveryPercent;
    public int CreatorPercent { get; set; } = EarningsRules.DefaultContentPercent;
}

public static class EarningsLedger
{
    public static async Task<EarningsOptions> AllocationAsync(CoLearnXDbContext db, Course course,
        EarningsOptions fallback, CancellationToken ct)
    {
        var pool = await db.PricePools.AsNoTracking()
            .Where(row => row.CourseLevelId == course.CourseLevelId
                && db.CourseSubjects.Any(subject => subject.Id == course.CourseSubjectId && subject.Subject == row.Subject))
            .Select(row => new { row.DeliverySharePercent, row.ContentSharePercent, row.PlatformSharePercent })
            .SingleOrDefaultAsync(ct);
        if (pool is null) return fallback;
        if (pool.DeliverySharePercent < 0 || pool.ContentSharePercent < 0 || pool.PlatformSharePercent < 0
            || (long)pool.DeliverySharePercent + pool.ContentSharePercent + pool.PlatformSharePercent != 100)
            throw new InvalidOperationException("The course price pool has an invalid earnings allocation.");
        return new EarningsOptions { TrainerPercent = pool.DeliverySharePercent, CreatorPercent = pool.ContentSharePercent };
    }

    public static async Task RecordCompletionAsync(CoLearnXDbContext db, CourseIntake intake, Course course,
        IReadOnlyCollection<Enrollment> enrollments, EarningsOptions fallback, DateTime now, CancellationToken ct)
    {
        var allocation = await AllocationAsync(db, course, fallback, ct);
        var enrollmentIds = enrollments.Select(e => e.Id).ToList();
        var existing = await db.Earnings.Where(x => enrollmentIds.Contains(x.EnrollmentId))
            .Select(x => new { x.EnrollmentId, x.Role }).ToListAsync(ct);
        foreach (var enrollment in enrollments.Where(x => x.Status is EnrollmentStatus.Active or EnrollmentStatus.Completed))
        {
            Add(EarningRole.Creator, course.CreatorId, allocation.CreatorPercent);
            Add(EarningRole.Trainer, intake.TrainerId, allocation.TrainerPercent);

            void Add(EarningRole role, int userId, int percent)
            {
                if (percent <= 0 || existing.Any(x => x.EnrollmentId == enrollment.Id && x.Role == role)) return;
                var credits = checked((int)((long)enrollment.CreditsSpent * percent / 100));
                if (credits <= 0) return;
                db.Earnings.Add(new Earning
                {
                    UserId = userId, CourseId = course.Id, CourseIntakeId = intake.Id,
                    EnrollmentId = enrollment.Id, Role = role, Credits = credits,
                    SharePercent = percent, Status = EarningStatus.Pending, CreatedAt = now,
                    AvailableAt = now.AddDays(EarningsRules.CooldownDays)
                });
            }
        }
    }

    public static async Task ReleaseDueAsync(CoLearnXDbContext db, DateTime now, CancellationToken ct)
        => _ = await db.Earnings.Where(x => x.Status == EarningStatus.Pending && x.AvailableAt <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Status, EarningStatus.Available), ct);

    public static async Task VoidRefundableAsync(CoLearnXDbContext db, int enrollmentId, string reason, CancellationToken ct,
        int? userId = null, int? adminAccountId = null)
    {
        var rows = await db.Earnings.Where(x => x.EnrollmentId == enrollmentId
            && (x.Status == EarningStatus.Pending || x.Status == EarningStatus.Available)).ToListAsync(ct);
        foreach (var row in rows) row.Status = EarningStatus.Voided;
        var protectedCount = await db.Earnings.CountAsync(x => x.EnrollmentId == enrollmentId
            && (x.Status == EarningStatus.Requested || x.Status == EarningStatus.Paid), ct);
        if (rows.Count > 0 || protectedCount > 0)
        {
            if ((userId.HasValue ? 1 : 0) + (adminAccountId.HasValue ? 1 : 0) != 1)
                throw new InvalidOperationException("Voiding earnings requires exactly one audit actor.");
            db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                AdminAccountId = adminAccountId,
                Action = "EnrollmentEarningsVoided", EntityType = nameof(Enrollment), EntityId = enrollmentId.ToString(),
                Result = protectedCount > 0 ? "ManualReview" : "Succeeded",
                Reason = protectedCount > 0 ? $"{reason}; {protectedCount} requested or paid earning rows require manual handling." : reason
            });
        }
    }

}
