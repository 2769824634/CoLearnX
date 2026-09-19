namespace CoLearnX.Server.Contracts.Dtos;

public record NotificationDto(int Id, string Type, string Title, string Message,
    DateTime CreatedAt, bool IsRead, string? TargetPath);

public record NotificationInboxDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);
