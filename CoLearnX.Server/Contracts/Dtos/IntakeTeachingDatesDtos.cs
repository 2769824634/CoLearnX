using System.Text.Json.Serialization;

namespace CoLearnX.Server.Contracts.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record UpdateIntakeTeachingDatesRequest(
    [property: JsonRequired] DateTime StartsAt,
    [property: JsonRequired] DateTime EndsAt,
    [property: JsonRequired] Guid Version);

public record AdminIntakeDatesDto(int Id, int CourseId, string CourseCode, string CourseTitle,
    string TrainerName, DateTime StartsAt, DateTime EndsAt, string Status);
