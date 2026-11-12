using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IEarningsService
{
    Task<EarningsSummaryDto> GetAsync(int userId, AppRole role, CancellationToken ct = default);
}

public sealed class EarningsService(CoLearnXDbContext db) : IEarningsService
{
    public async Task<EarningsSummaryDto> GetAsync(int userId, AppRole role, CancellationToken ct = default)
    {
        var earningRole = role == AppRole.Creator ? EarningRole.Creator : EarningRole.Trainer;
        await EarningsLedger.ReleaseDueAsync(db, DateTime.UtcNow, ct);
        var entries = await db.Earnings.AsNoTracking().Where(x => x.UserId == userId && x.Role == earningRole)
            .Select(x => new { x.CourseIntakeId, x.Course.Code, x.Course.Title, x.CourseIntake.StartsAt,
                x.CourseIntake.EndsAt, x.EnrollmentId, x.Credits, x.SharePercent, x.Status, x.AvailableAt, x.CreatedAt })
            .ToListAsync(ct);
        var payouts = await db.PayoutRequests.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RequestedAt).Select(x => new PayoutRequestDto(x.Id, x.Credits,
                x.PayoutMethod, x.Status.ToString(), x.RequestedAt, x.ReviewedAt, x.Reference)).ToListAsync(ct);
        var active = entries.Where(x => x.Status != EarningStatus.Voided).ToList();
        var classes = entries.GroupBy(x => x.CourseIntakeId).Select(group =>
        {
            var statuses = group.Select(x => x.Status).Distinct().ToList();
            return new EarningClassDto(group.Key, group.First().Code, group.First().Title, group.First().EndsAt,
                group.Select(x => x.EnrollmentId).Distinct().Count(), group.Sum(x => (long)x.Credits),
                group.Select(x => x.SharePercent).Distinct().Count() == 1 ? group.First().SharePercent : null,
                statuses.Count == 1 ? statuses[0].ToString() : "Mixed", group.Max(x => x.AvailableAt));
        }).OrderByDescending(x => x.EndsAt).ToList();
        var rows = classes.Select(x => new EarningRowDto(x.CourseIntakeId, x.CourseCode, x.CourseTitle, x.EndsAt,
            x.Learners, x.Credits, x.Credits, false, x.EndsAt)).ToList();
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new EarningsSummaryDto(active.Sum(x => (long)x.Credits),
            active.Where(x => x.Status == EarningStatus.Pending).Sum(x => (long)x.Credits),
            active.Where(x => x.Status == EarningStatus.Available).Sum(x => (long)x.Credits),
            active.Where(x => x.Status == EarningStatus.Requested).Sum(x => (long)x.Credits),
            active.Where(x => x.Status == EarningStatus.Paid).Sum(x => (long)x.Credits),
            EarningsRules.MinimumPayoutCredits, EarningsRules.CashPerCredit, classes, payouts,
            active.Where(x => x.CreatedAt >= month).Sum(x => (long)x.Credits), classes.Count,
            active.Count > 0 && active.Select(x => x.SharePercent).Distinct().Count() == 1 ? active[0].SharePercent : null,
            rows);
    }
}
