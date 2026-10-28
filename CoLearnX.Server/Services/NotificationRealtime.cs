using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CoLearnX.Server.Services;

public interface INotificationRealtime
{
    Task PublishAsync(Notification notification, CancellationToken ct = default);
}

public sealed class NotificationRealtime(IHubContext<NotificationHub> hub, ILogger<NotificationRealtime> logger) : INotificationRealtime
{
    private static readonly HashSet<string> Important = new(StringComparer.Ordinal)
    {
        "N-course-published",
        "N-course-rejected",
        "N-course-invitation",
        "N-course-invitation-accepted",
        "N-course-invitation-declined",
        "N-intake-application-confirmed",
        "N-intake-application-rejected",
        "N-role-approved",
        "N-role-rejected",
    };

    public async Task PublishAsync(Notification notification, CancellationToken ct)
    {
        if (notification.Id <= 0 || !Important.Contains(notification.Code))
            return;
        var dto = new NotificationDto(
            notification.Id,
            notification.Code,
            notification.Title,
            notification.Body,
            notification.CreatedAt,
            notification.IsRead,
            NotificationTargetPath.For(notification.Code, notification.IntakeId, notification.EnrollmentId),
            notification.IntakeId,
            notification.EnrollmentId);
        try
        {
            await hub.Clients.User(notification.UserId.ToString()).SendAsync("notification", dto, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Notification {NotificationId} was saved but could not be pushed.", notification.Id);
        }
    }
}
