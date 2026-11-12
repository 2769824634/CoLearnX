namespace CoLearnX.Server.Domain.Entities;

public sealed class EnrollmentSessionChoice
{
    public int EnrollmentId { get; set; }
    public int CourseSessionId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public Enrollment Enrollment { get; set; } = null!;
    public CourseSession CourseSession { get; set; } = null!;
}
