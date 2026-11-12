using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed class EarningsOptions
{
    public int TrainerPercent { get; set; } = 25;
    public int CreatorPercent { get; set; } = 25;
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

    // Cumulative allocation keeps the class totals exact even when individual amounts are rounded.
    public static void RecordSettlement(CoLearnXDbContext db, CourseIntake intake, Course course,
        IEnumerable<Enrollment> enrollments, EarningsOptions options)
    {
        long gross = 0;
        foreach (var enrollment in enrollments.OrderBy(item => item.Id))
        {
            var next = gross + enrollment.CreditsSpent;
            Add(AppRole.Trainer, intake.TrainerId, options.TrainerPercent);
            Add(AppRole.Creator, course.CreatorId, options.CreatorPercent);
            gross = next;

            void Add(AppRole role, int recipient, int percent) => db.IntakeEarnings.Add(new IntakeEarning
            {
                CourseIntakeId = intake.Id, UserId = recipient, Role = role, EnrollmentId = enrollment.Id,
                Credits = checked((int)(next * percent / 100 - gross * percent / 100)),
                GrossCredits = enrollment.CreditsSpent,
            });
        }
    }

    public static async Task ReverseAsync(CoLearnXDbContext db, int enrollmentId, int refundCredits, CancellationToken ct)
    {
        if (refundCredits <= 0) return;
        var rows = await db.IntakeEarnings.Where(item => item.EnrollmentId == enrollmentId).ToListAsync(ct);
        foreach (var original in rows.Where(item => !item.IsReversal))
        {
            if (original.GrossCredits <= 0) continue;
            var previous = rows.Where(item => item.IsReversal && item.Role == original.Role).ToList();
            var priorGross = -previous.Sum(item => item.GrossCredits);
            var cumulativeGross = Math.Min((long)original.GrossCredits, (long)priorGross + refundCredits);
            var target = (int)((long)original.Credits * cumulativeGross / original.GrossCredits);
            var reversed = -previous.Sum(item => item.Credits);
            db.IntakeEarnings.Add(new IntakeEarning
            {
                CourseIntakeId = original.CourseIntakeId, UserId = original.UserId, Role = original.Role,
                EnrollmentId = enrollmentId, IsReversal = true, Credits = -(target - reversed),
                GrossCredits = -(int)(cumulativeGross - priorGross),
            });
        }
    }
}
