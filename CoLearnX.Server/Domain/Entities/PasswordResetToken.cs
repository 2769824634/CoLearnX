namespace CoLearnX.Server.Domain.Entities;

// One current credential per ordinary user; never stores the emailed secret.
public class PasswordResetToken
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
