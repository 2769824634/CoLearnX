namespace CoLearnX.Server.Contracts.Dtos;

public record NotificationDto(int Id, string Type, string Title, string Message,
    DateTime CreatedAt, bool IsRead, string? TargetPath, int? IntakeId = null, int? EnrollmentId = null);

public record NotificationInboxDto(IReadOnlyList<NotificationDto> Items, int UnreadCount);
