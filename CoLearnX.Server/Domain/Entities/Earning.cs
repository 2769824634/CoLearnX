using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Domain.Entities;

public sealed class Earning
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CourseId { get; set; }
    public int CourseIntakeId { get; set; }
    public int EnrollmentId { get; set; }
    public EarningRole Role { get; set; }
    public int Credits { get; set; }
    public int SharePercent { get; set; }
    public EarningStatus Status { get; set; } = EarningStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime AvailableAt { get; set; }
    public int? PayoutRequestId { get; set; }
    public User User { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public CourseIntake CourseIntake { get; set; } = null!;
    public Enrollment Enrollment { get; set; } = null!;
    public PayoutRequest? PayoutRequest { get; set; }
}

public sealed class PayoutRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Credits { get; set; }
    public string PayoutMethod { get; set; } = string.Empty;
    public PayoutStatus Status { get; set; } = PayoutStatus.Requested;
    public string? Reference { get; set; }
    public int? ReviewedByAdminAccountId { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public User User { get; set; } = null!;
    public AdminAccount? ReviewedByAdminAccount { get; set; }
    public ICollection<Earning> Earnings { get; set; } = new List<Earning>();
}
