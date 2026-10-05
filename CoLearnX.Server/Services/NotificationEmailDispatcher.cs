using CoLearnX.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IBusinessNotificationMailSender
{
    bool IsAvailable { get; }
    Task SendAsync(string email, string title, string body, string targetPath, CancellationToken ct);
}

public sealed class NotificationEmailDispatcher(CoLearnXDbContext db, IBusinessNotificationMailSender mail,
    ILogger<NotificationEmailDispatcher> logger)
{
    public async Task DispatchPendingAsync(CancellationToken ct = default)
    {
        if (!mail.IsAvailable) return;
        // A short database lease prevents simultaneous workers from sending the same row.
        // SMTP is outside the financial transaction; a failed attempt is retried after five minutes.
        var now = DateTime.UtcNow;
        var retryBefore = now.AddMinutes(-5);
        var pending = await db.Notifications.AsNoTracking().Include(n => n.User).ThenInclude(u => u.Preference)
            .Where(n => n.EmailPending && (n.EmailAttemptedAt == null || n.EmailAttemptedAt < retryBefore))
            .OrderBy(n => n.Id).Take(50).ToListAsync(ct);
        foreach (var notice in pending)
        {
            var attemptedAt = DateTime.UtcNow;
            var claimed = await db.Notifications.Where(n => n.Id == notice.Id && n.EmailPending
                    && (n.EmailAttemptedAt == null || n.EmailAttemptedAt < retryBefore))
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailAttemptedAt, attemptedAt), ct);
            if (claimed != 1) continue;
            var ownedLease = db.Notifications.Where(n => n.Id == notice.Id && n.EmailPending && n.EmailAttemptedAt == attemptedAt);
            var path = NotificationTargetPath.For(notice.Code, notice.IntakeId);
            var cancelled = notice.IntakeId != null && (notice.Code is "N-class-confirmed" or "N-class-reminder")
                && await db.CourseIntakes.AnyAsync(i => i.Id == notice.IntakeId && i.CancelledAt != null, ct);
            if (!notice.User.IsActive || notice.User.Preference?.EmailNotifications == false || path == null || cancelled)
            {
                await ownedLease.ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailPending, false), ct);
                continue;
            }
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                await mail.SendAsync(notice.User.Email, notice.Title, notice.Body, path, timeout.Token);
                await ownedLease.ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailPending, false)
                    .SetProperty(n => n.EmailSentAt, DateTime.UtcNow), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // Do not put recipient addresses, meeting links or SMTP credentials in logs.
                logger.LogWarning("Business email delivery failed for notification {NotificationId}; error={Error}", notice.Id, ex.GetType().Name);
            }
        }
    }
}
