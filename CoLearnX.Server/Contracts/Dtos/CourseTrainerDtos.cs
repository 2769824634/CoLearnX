using System.ComponentModel.DataAnnotations;

namespace CoLearnX.Server.Contracts.Dtos;

public record CourseTrainerDto(int TrainerId, string FullName, string? Headline, string Status,
    DateTime InvitedAt, DateTime? RespondedAt, DateTime? RemovedAt);

public record TrainerCandidateDto(int Id, string FullName, string? Headline, string? Specialisations, IReadOnlyList<string>? Specialties = null);

public record CourseInvitationDto(int Id, int CourseId, string CourseCode, string CourseTitle, string CreatorName,
    string Status, DateTime InvitedAt, DateTime? RespondedAt, DateTime? RemovedAt);

