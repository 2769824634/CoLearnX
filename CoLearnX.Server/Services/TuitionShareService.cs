using CoLearnX.Server.Data;
using CoLearnX.Server.Domain;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

// Confirmed tuition is split once. Refunds move the same credits back.
internal sealed class TuitionShareService(CoLearnXDbContext db)
{
    public const string Content = "Content share";
    public const string Delivery = "Delivery share";
    public const string DeliveryRetained = "Delivery share retained";
    public const string Platform = "Platform share";
    private const int DefaultContentPercent = 25;
    private const int DefaultDeliveryPercent = 25;

    public async Task DistributeAsync(int courseId, int trainerId, int intakeId, IReadOnlyList<Enrollment> enrollments,
        CancellationToken ct)
    {
        if (enrollments.Count == 0) return;
        var ids = enrollments.Select(item => item.Id).ToArray();
        var alreadyShared = await db.CreditTransactions.AsNoTracking()
            .Where(item => item.RelatedEnrollmentId != null && ids.Contains(item.RelatedEnrollmentId.Value)
                && item.Type == CreditTransactionType.Royalty && item.Delta > 0)
            .Select(item => item.RelatedEnrollmentId!.Value)
            .ToListAsync(ct);
        var pending = enrollments.Where(item => item.CreditsSpent > 0 && !alreadyShared.Contains(item.Id)).ToList();
        if (pending.Count == 0) return;

        var course = await db.Courses.AsNoTracking().SingleAsync(item => item.Id == courseId, ct);
        var (contentPercent, deliveryPercent) = await PercentsAsync(course, ct);
        var platform = await db.Users.SingleAsync(item => item.Email == PlatformIdentity.Email, ct);
        var trainerIsStaff = await db.StaffAccounts.AnyAsync(item => item.UserId == trainerId && item.IsActive, ct);
        var creator = course.CreatorId == platform.Id
            ? platform
            : await db.Users.SingleAsync(item => item.Id == course.CreatorId, ct);
        var trainer = trainerId == platform.Id
            ? platform
            : trainerId == creator.Id
                ? creator
                : await db.Users.SingleAsync(item => item.Id == trainerId, ct);
        var totals = new Dictionary<int, (User User, int Content, int Delivery, int Retained, int Platform)>();

        foreach (var enrollment in pending)
        {
            var content = enrollment.CreditsSpent * contentPercent / 100;
            var delivery = enrollment.CreditsSpent * deliveryPercent / 100;
            var platformAmount = enrollment.CreditsSpent - content - delivery;
            if (content < 0 || delivery < 0 || platformAmount < 0)
            {
                content = 0;
                delivery = 0;
                platformAmount = enrollment.CreditsSpent;
            }
            var retained = 0;
            if (trainerIsStaff)
            {
                retained = delivery;
                delivery = 0;
            }
            Add(totals, creator, enrollment, course.Code, Content, content);
            Add(totals, trainer, enrollment, course.Code, Delivery, delivery);
            Add(totals, platform, enrollment, course.Code, DeliveryRetained, retained);
            Add(totals, platform, enrollment, course.Code, Platform, platformAmount);
        }

        foreach (var share in totals.Values)
        {
            if (share.User.Email == PlatformIdentity.Email) continue;
            var parts = new List<string>();
            if (share.Content > 0) parts.Add($"{BusinessText.Credits(share.Content)} content");
            if (share.Delivery > 0) parts.Add($"{BusinessText.Credits(share.Delivery)} delivery");
            db.Notifications.Add(new Notification
            {
                UserId = share.User.Id,
                IntakeId = intakeId,
                EmailPending = false,
                Code = "N-share-recorded",
                Title = "Credit share recorded",
                Body = $"{course.Code}: {string.Join(" and ", parts)} added to your balance.",
            });
        }
    }

    public async Task ClawBackAsync(Enrollment enrollment, int refundCredits, CancellationToken ct)
    {
        if (refundCredits <= 0 || enrollment.CreditsSpent <= 0) return;
        var rows = await db.CreditTransactions.AsNoTracking()
            .Where(item => item.RelatedEnrollmentId == enrollment.Id && item.Type == CreditTransactionType.Royalty)
            .ToListAsync(ct);
        var buckets = rows.Select(item => (item.UserId, Role: RoleOf(item.Description), item.Delta))
            .Where(item => item.Role is not null)
            .GroupBy(item => (item.UserId, Role: item.Role!))
            .Select(group => new Bucket(group.Key.UserId, group.Key.Role, group.Where(item => item.Delta > 0).Sum(item => item.Delta), group.Sum(item => item.Delta)))
            .Where(item => item.Original > 0)
            .ToList();
        if (buckets.Count == 0) return;

        var alreadyRefunded = await db.CreditTransactions.AsNoTracking()
            .Where(item => item.RelatedEnrollmentId == enrollment.Id && item.Type == CreditTransactionType.Refund)
            .SumAsync(item => (int?)item.Delta, ct) ?? 0;
        var retainedTuition = Math.Max(0, enrollment.CreditsSpent - alreadyRefunded - refundCredits);
        var remainder = buckets.FirstOrDefault(item => item.Role == Platform) ?? buckets[^1];
        var assigned = 0;
        var targets = new Dictionary<Bucket, int>();
        foreach (var bucket in buckets.Where(item => item != remainder))
        {
            var target = (int)((long)bucket.Original * retainedTuition / enrollment.CreditsSpent);
            targets[bucket] = target;
            assigned += target;
        }
        targets[remainder] = retainedTuition - assigned;

        var code = enrollment.Course?.Code ?? await db.Courses.Where(item => item.Id == enrollment.CourseId).Select(item => item.Code).SingleAsync(ct);
        var userIds = buckets.Select(item => item.UserId).Distinct().ToArray();
        var users = await db.Users.Where(item => userIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        var reversed = new Dictionary<int, int>();
        foreach (var bucket in buckets)
        {
            var delta = targets[bucket] - bucket.Current;
            if (delta == 0) continue;
            var user = users[bucket.UserId];
            user.CreditBalance += delta;
            db.CreditTransactions.Add(new CreditTransaction
            {
                UserId = user.Id,
                Type = CreditTransactionType.Royalty,
                Description = $"Reversal of {bucket.Role} · {code} · enrollment {enrollment.Id}",
                Delta = delta,
                BalanceAfter = user.CreditBalance,
                HeldAfter = user.HeldCredits,
                RelatedEnrollmentId = enrollment.Id,
            });
            if (delta < 0 && user.Email != PlatformIdentity.Email)
                reversed[user.Id] = reversed.GetValueOrDefault(user.Id) + -delta;
        }
        foreach (var (userId, amount) in reversed)
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                EmailPending = false,
                Code = "N-share-reversed",
                Title = "Credit share reversed",
                Body = $"{code}: {BusinessText.Credits(amount)} removed after a refund.",
            });
        }
    }

    private void Add(Dictionary<int, (User User, int Content, int Delivery, int Retained, int Platform)> totals,
        User user, Enrollment enrollment, string code, string role, int amount)
    {
        if (amount == 0) return;
        user.CreditBalance += amount;
        db.CreditTransactions.Add(new CreditTransaction
        {
            UserId = user.Id,
            Type = CreditTransactionType.Royalty,
            Description = $"{role} · {code} · enrollment {enrollment.Id}",
            Delta = amount,
            BalanceAfter = user.CreditBalance,
            HeldAfter = user.HeldCredits,
            RelatedEnrollmentId = enrollment.Id,
        });
        totals.TryGetValue(user.Id, out var share);
        share.User = user;
        share.Content += role == Content ? amount : 0;
        share.Delivery += role == Delivery ? amount : 0;
        share.Retained += role == DeliveryRetained ? amount : 0;
        share.Platform += role == Platform ? amount : 0;
        totals[user.Id] = share;
    }

    private async Task<(int Content, int Delivery)> PercentsAsync(Course course, CancellationToken ct)
    {
        if (course.CourseSubjectId is not int subjectId || course.CourseLevelId is not int levelId)
            return (DefaultContentPercent, DefaultDeliveryPercent);
        var area = await db.CourseSubjects.Where(item => item.Id == subjectId).Select(item => (SubjectArea?)item.Subject).SingleOrDefaultAsync(ct);
        if (area is null) return (DefaultContentPercent, DefaultDeliveryPercent);
        var cell = await db.PricePools.AsNoTracking().FirstOrDefaultAsync(item => item.Subject == area && item.CourseLevelId == levelId, ct);
        if (cell is null || cell.ContentSharePercent < 0 || cell.DeliverySharePercent < 0
            || cell.ContentSharePercent + cell.DeliverySharePercent > 100)
            return (DefaultContentPercent, DefaultDeliveryPercent);
        return (cell.ContentSharePercent, cell.DeliverySharePercent);
    }

    private static string? RoleOf(string description)
    {
        var text = description.StartsWith("Reversal of ", StringComparison.Ordinal)
            ? description["Reversal of ".Length..]
            : description;
        if (text.StartsWith(DeliveryRetained, StringComparison.Ordinal)) return DeliveryRetained;
        if (text.StartsWith(Content, StringComparison.Ordinal)) return Content;
        if (text.StartsWith(Delivery, StringComparison.Ordinal)) return Delivery;
        if (text.StartsWith(Platform, StringComparison.Ordinal)) return Platform;
        return null;
    }

    private sealed record Bucket(int UserId, string Role, int Original, int Current);
}
