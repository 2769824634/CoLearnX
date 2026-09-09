using System.ComponentModel.DataAnnotations;

namespace CoLearnX.Server.Contracts.Dtos;

public record CreateCreatorCourseRequest(
    [Required, MaxLength(64)] string Code,
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string? Description,
    [Range(1, int.MaxValue)] int CourseLevelId,
    [Range(1, int.MaxValue)] int LearningPathId,
    [Required, MaxLength(100)] string Category,
    [Range(1, int.MaxValue)] int CreditCost,
    IReadOnlyList<string>? LearningOutcomes);

public record UpdateCreatorCourseRequest(
    [Required, MaxLength(64)] string Code,
    [Required, MaxLength(200)] string Title,
    [MaxLength(4000)] string? Description,
    [Range(1, int.MaxValue)] int CourseLevelId,
    [Range(1, int.MaxValue)] int LearningPathId,
    [Required, MaxLength(100)] string Category,
    [Range(1, int.MaxValue)] int CreditCost,
    IReadOnlyList<string>? LearningOutcomes);

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
    string? ReviewReason);

public record CourseOptionDto(int Id, string Name);

public record CreatorCourseOptionsDto(
    IReadOnlyList<CourseOptionDto> CourseLevels,
    IReadOnlyList<CourseOptionDto> LearningPaths);
