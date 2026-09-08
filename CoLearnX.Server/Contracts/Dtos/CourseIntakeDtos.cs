using System.Text.Json.Serialization;

namespace CoLearnX.Server.Contracts.Dtos;

// UTC timestamps; ownership, status and confirmation fields are never client-writable.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateCourseIntakeRequest([property: JsonRequired] DateTime RegistrationOpensAt,
    [property: JsonRequired] DateTime RegistrationClosesAt, [property: JsonRequired] DateTime StartsAt, [property: JsonRequired] DateTime EndsAt);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateCourseIntakeRequest([property: JsonRequired] DateTime RegistrationOpensAt,
    [property: JsonRequired] DateTime RegistrationClosesAt, [property: JsonRequired] DateTime StartsAt,
    [property: JsonRequired] DateTime EndsAt, [property: JsonRequired] Guid Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateCourseSessionRequest([property: JsonRequired] string Label, [property: JsonRequired] DateTime StartsAt,
    [property: JsonRequired] DateTime EndsAt, string? MeetingLink,
    string? PhysicalAddress, int PhysicalCapacity, DateTime? PhysicalBookingDeadline, [property: JsonRequired] Guid Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateCourseSessionRequest([property: JsonRequired] string Label, [property: JsonRequired] DateTime StartsAt,
    [property: JsonRequired] DateTime EndsAt, string? MeetingLink,
    string? PhysicalAddress, int PhysicalCapacity, DateTime? PhysicalBookingDeadline, [property: JsonRequired] Guid Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record SubmitCourseIntakeRequest([property: JsonRequired] Guid Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateSessionDeliveryRequest([property: JsonRequired] string? MeetingLink,
    [property: JsonRequired] Guid Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record ProposedCourseSessionRequest(int? Id, [property: JsonRequired] string Label,
    [property: JsonRequired] DateTime StartsAt, [property: JsonRequired] DateTime EndsAt,
    string? MeetingLink, string? PhysicalAddress, int PhysicalCapacity, DateTime? PhysicalBookingDeadline);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CreateCourseIntakeChangeRequest([property: JsonRequired] DateTime RegistrationOpensAt,
    [property: JsonRequired] DateTime RegistrationClosesAt, [property: JsonRequired] DateTime StartsAt,
    [property: JsonRequired] DateTime EndsAt, [property: JsonRequired] IReadOnlyList<ProposedCourseSessionRequest> Sessions,
    [property: JsonRequired] Guid Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record ReviewIntakeApplicationRequest([property: JsonRequired] string Decision,
    string? ConfirmationNote, [property: JsonRequired] Guid Version);
public record CourseIntakeSummaryDto(int Id, int CourseId, int TrainerId, DateTime StartsAt, DateTime EndsAt, string Status, Guid Version);
public record CourseIntakeChangeDto(int ApplicationId, string Status, string RestoreStatus, DateTime SubmittedAt,
    string? ReviewNote, DateTime RegistrationOpensAt, DateTime RegistrationClosesAt, DateTime StartsAt,
    DateTime EndsAt, IReadOnlyList<ProposedCourseSessionRequest> Sessions);
public record CourseIntakeDetailDto(int Id, int CourseId, int TrainerId, DateTime RegistrationOpensAt,
    DateTime RegistrationClosesAt, DateTime StartsAt, DateTime EndsAt, string Status, DateTime? SubmittedAt,
    int? ConfirmedByCreatorId, DateTime? ConfirmedAt, string? ConfirmationNote, Guid Version,
    IReadOnlyList<CourseSessionDto> Sessions, CourseIntakeChangeDto? LatestChangeRequest = null);
public record CreatorIntakeApplicationSummaryDto(int CourseIntakeId, int ApplicationId, int CourseId,
    string CourseCode, string CourseTitle, int TrainerId, string TrainerName, string Kind, string Status,
    DateTime SubmittedAt, Guid Version);
public record CreatorIntakeApplicationDetailDto(CreatorIntakeApplicationSummaryDto Application,
    CourseIntakeDetailDto CurrentIntake, CourseIntakeChangeDto? ProposedChange);
