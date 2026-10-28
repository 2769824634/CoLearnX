namespace CoLearnX.Server.Domain.Entities;

public sealed class StaffAccount
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid SessionStamp { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int StaffNumber { get; set; }
    public string DocumentPath { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int CreatedByAdminAccountId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public AdminAccount CreatedByAdminAccount { get; set; } = null!;
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
