using System.ComponentModel.DataAnnotations;

namespace CoLearnX.Server.Contracts.Dtos;

public record CreateCreatorCourseRequest(
    [Required, MaxLength(64)] string Code,
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string? Description,
    [Range(1, int.MaxValue)] int CourseLevelId,
    [Range(1, int.MaxValue)] int LearningPathId,
    [MaxLength(100)] string? Category,
    [Range(1, int.MaxValue)] int CreditCost,
    IReadOnlyList<string>? LearningOutcomes,
    IReadOnlyList<int>? InterestIds = null,
    int CourseSubjectId = 0,
    int DurationHours = 0,
    string? PriceExceptionReason = null);

public record UpdateCreatorCourseRequest(
    [Required, MaxLength(64)] string Code,
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string? Description,
    [Range(1, int.MaxValue)] int CourseLevelId,
    [Range(1, int.MaxValue)] int LearningPathId,
    [MaxLength(100)] string? Category,
    [Range(1, int.MaxValue)] int CreditCost,
    IReadOnlyList<string>? LearningOutcomes,
    IReadOnlyList<int>? InterestIds = null,
    int CourseSubjectId = 0,
    int DurationHours = 0,
    string? PriceExceptionReason = null);

public record CreatorCourseDto(
    int Id,
    string Code,
    string Title,
    string? Description,
    int CreatorId,
    string CreatorEmail,
    string CreatorName,
    int CourseLevelId,
    string CourseLevelName,
    int LearningPathId,
    string LearningPathName,
    string Category,
    int CreditCost,
    string Status,
    IReadOnlyList<string> LearningOutcomes,
    DateTime CreatedAt,
    string? ReviewReason,
    IReadOnlyList<int>? InterestIds = null,
    bool IsPlatformOwned = false,
    int? CourseSubjectId = null,
    string? CourseSubjectName = null,
    int? DurationHours = null,
    string? PriceExceptionReason = null);

public record CourseOptionDto(int Id, string Name);

public record CourseSubjectOptionDto(int Id, string Name, string Band);

public record PriceCellDto(string Band, int CourseLevelId, int MinCredits, int MaxCredits);

public record CreatorCourseOptionsDto(
    IReadOnlyList<CourseOptionDto> CourseLevels,
    IReadOnlyList<CourseOptionDto> LearningPaths,
    IReadOnlyList<CourseSubjectOptionDto>? CourseSubjects = null,
    IReadOnlyList<PriceCellDto>? PriceCells = null);
