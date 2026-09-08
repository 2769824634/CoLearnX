using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Domain.Entities;

// Immutable application input plus its review outcome. Change proposals are kept
// separately so a published delivery schedule is never overwritten before approval.
public class CourseIntakeApplication
{
    public int Id { get; set; }
    public int CourseIntakeId { get; set; }
    public Guid ApplicationVersion { get; set; }
    public CourseIntakeApplicationKind Kind { get; set; }
    public CourseIntakeApplicationStatus Status { get; set; } = CourseIntakeApplicationStatus.Pending;
    public CourseIntakeStatus? RestoreStatus { get; set; }
    public string? ProposalJson { get; set; }
    public int SubmittedByTrainerId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int? ReviewedByCreatorId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public CourseIntake CourseIntake { get; set; } = null!;
    public User SubmittedByTrainer { get; set; } = null!;
    public User? ReviewedByCreator { get; set; }
}
