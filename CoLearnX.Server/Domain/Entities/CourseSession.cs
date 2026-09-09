namespace CoLearnX.Server.Domain.Entities;

public class CourseSession
{
    public int Id { get; set; }
    public int CourseIntakeId { get; set; }
    public string Label { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public string? MeetingLink { get; set; }
    public string? PhysicalAddress { get; set; }
    public int PhysicalCapacity { get; set; }
    public DateTime? PhysicalBookingDeadline { get; set; }
    // Legacy Member seat counter, pending A's Intake enrolment / physical reservation split.
    public int SeatsTaken { get; set; }
    public int SeatsLeft => Math.Max(0, PhysicalCapacity - SeatsTaken);
    public CourseIntake CourseIntake { get; set; } = null!;
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
