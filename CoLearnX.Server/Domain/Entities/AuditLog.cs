namespace CoLearnX.Server.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public int? AdminAccountId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AdminAccount? AdminAccount { get; set; }
    public User? User { get; set; }
}
