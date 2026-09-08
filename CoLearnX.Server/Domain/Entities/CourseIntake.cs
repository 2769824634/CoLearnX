using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Domain.Entities;

public class CourseIntake
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int TrainerId { get; set; }
    public DateTime RegistrationOpensAt { get; set; }
    public DateTime RegistrationClosesAt { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public CourseIntakeStatus Status { get; set; } = CourseIntakeStatus.Draft;
    public DateTime? SubmittedAt { get; set; }
    public int? ConfirmedByCreatorId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmationNote { get; set; }
    // One concurrency token protects the whole Intake, including Session structure.
    public Guid Version { get; set; } = Guid.NewGuid();
    public Course Course { get; set; } = null!;
    public User Trainer { get; set; } = null!;
    public User? ConfirmedByCreator { get; set; }
    public ICollection<CourseSession> Sessions { get; set; } = new List<CourseSession>();
    public ICollection<CourseIntakeApplication> Applications { get; set; } = new List<CourseIntakeApplication>();
}
