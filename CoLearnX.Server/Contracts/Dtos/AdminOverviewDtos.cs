namespace CoLearnX.Server.Contracts.Dtos;

// One row of the admin class overview. AttendanceRate is null until a session has been held.
public record AdminClassRowDto(
    int Id,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    int CreatorId,
    string CreatorName,
    int TrainerId,
    string? TrainerName, // null while the Intake has no Trainer yet
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    int Learners,
    int MinEnrollment,
    int SessionCount,
    int SessionsHeld,
    double? AttendanceRate);

public record AdminClassSessionDto(int Id, string Label, DateTime StartsAt, DateTime EndsAt, bool Held, int Attended, int Learners);

public record AdminClassLearnerDto(
    int UserId,
    string FullName,
    string Email,
    string EnrollmentStatus,
    int Attended,
    int SessionsHeld,
    double? AttendanceRate,
    string? CertificateStatus);

public record AdminClassMaterialDto(string Title, string Scope);

public record AdminClassDetailDto(
    AdminClassRowDto Class,
    IReadOnlyList<AdminClassSessionDto> Sessions,
    IReadOnlyList<AdminClassLearnerDto> Learners,
    IReadOnlyList<AdminClassMaterialDto> Materials);

public record AdminCreatorCourseDto(
    int CourseId,
    string Code,
    string Title,
    string Status,
    int ClassCount,
    int LearnerCount,
    IReadOnlyList<AdminClassRowDto> Classes);

public record AdminMemberCourseDto(
    int EnrollmentId,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    int? ClassId,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string Status,
    int CreditsSpent,
    int Attended,
    int SessionsHeld,
    double? AttendanceRate);

public record AdminMemberCertificateDto(int Id, string Title, string? CourseCode, DateTime AwardedAt);

public record AdminUserActivityDto(
    int UserId,
    IReadOnlyList<string> Roles,
    bool Certified,
    IReadOnlyList<string> Specialties,
    IReadOnlyList<AdminClassRowDto> TeachingClasses,
    IReadOnlyList<AdminCreatorCourseDto> CreatedCourses,
    int RoyaltyCredits,
    IReadOnlyList<AdminMemberCourseDto> Learning,
    IReadOnlyList<AdminMemberCertificateDto> Certificates,
    int TotalTopUpCredits,
    int TotalSpentCredits);
