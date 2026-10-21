using System.ComponentModel.DataAnnotations;

namespace CoLearnX.Server.Contracts.Dtos;

public record CourseTrainerDto(int TrainerId, string FullName, string? Headline, string Status,
    DateTime InvitedAt, DateTime? RespondedAt, DateTime? RemovedAt);

public record TrainerCandidateDto(int Id, string FullName, string? Headline, string? Specialisations);

public record CourseInvitationDto(int Id, int CourseId, string CourseCode, string CourseTitle, string CreatorName,
    string Status, DateTime InvitedAt, DateTime? RespondedAt, DateTime? RemovedAt);

public record InviteTrainerRequest([Range(1, int.MaxValue)] int TrainerId);
