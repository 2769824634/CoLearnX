using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Domain.Entities;

// Each row records one recipient's share of an enrollment or a refund of it.
public sealed class IntakeEarning
{
    public int Id { get; set; }
    public int CourseIntakeId { get; set; }
    public int UserId { get; set; }
    public AppRole Role { get; set; }
    public int EnrollmentId { get; set; }
    public int Credits { get; set; }
    public int GrossCredits { get; set; }
    public bool IsReversal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public CourseIntake CourseIntake { get; set; } = null!;
    public Enrollment Enrollment { get; set; } = null!;
    public User User { get; set; } = null!;
}
