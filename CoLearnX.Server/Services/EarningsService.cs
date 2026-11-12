using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoLearnX.Server.Services;

public sealed class EarningsService(CoLearnXDbContext db, IOptions<EarningsOptions> options)
{
    public async Task<EarningsSummaryDto> GetAsync(int userId, AppRole role, CancellationToken ct = default)
    {
        var entries = await db.IntakeEarnings.AsNoTracking().Where(row => row.UserId == userId && row.Role == role)
            .Select(row => new { row.CourseIntakeId, row.CourseIntake.Course.Code, row.CourseIntake.Course.Title,
                row.CourseIntake.StartsAt, row.EnrollmentId, row.Credits, row.GrossCredits, row.IsReversal, row.CreatedAt,
                row.CourseIntake.Course.CourseLevelId, Subject = row.CourseIntake.Course.CourseSubject == null
                    ? (SubjectArea?)null : row.CourseIntake.Course.CourseSubject.Subject })
            .ToListAsync(ct);
        var pools = await db.PricePools.AsNoTracking().Where(row => row.Subject != null)
            .Select(row => new { row.Subject, row.CourseLevelId, row.ContentSharePercent, row.DeliverySharePercent })
            .ToListAsync(ct);
        var rates = entries.Where(row => !row.IsReversal).Select(row =>
        {
            var pool = pools.SingleOrDefault(item => item.Subject == row.Subject && item.CourseLevelId == row.CourseLevelId);
            return pool is null ? (role == AppRole.Trainer ? options.Value.TrainerPercent : options.Value.CreatorPercent)
                : (role == AppRole.Trainer ? pool.DeliverySharePercent : pool.ContentSharePercent);
        }).Distinct().ToList();
        var classes = entries.Where(row => !row.IsReversal).GroupBy(row => row.CourseIntakeId)
            .Select(group => new EarningRowDto(group.Key, group.First().Code, group.First().Title, group.First().StartsAt,
                group.Count(), group.Sum(row => (long)row.GrossCredits), group.Sum(row => (long)row.Credits), false,
                group.Min(row => row.CreatedAt)));
        var refunds = entries.Where(row => row.IsReversal).Select(row => new EarningRowDto(row.CourseIntakeId,
            row.Code, row.Title, row.StartsAt, 0, row.GrossCredits, row.Credits, true, row.CreatedAt));
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new EarningsSummaryDto(entries.Sum(row => (long)row.Credits),
            entries.Where(row => row.CreatedAt >= month).Sum(row => (long)row.Credits),
            entries.Where(row => !row.IsReversal).Select(row => row.CourseIntakeId).Distinct().Count(),
            rates.Count == 1 ? rates[0] : null,
            classes.Concat(refunds).OrderByDescending(row => row.CreatedAt).ToList());
    }
}
