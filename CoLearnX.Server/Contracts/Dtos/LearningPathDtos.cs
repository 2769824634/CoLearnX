namespace CoLearnX.Server.Contracts.Dtos;

public record LearningPathCourseDto(int Id, string Code, string Title, int Credits, bool HasClass, bool Completed, bool Enrolled);
public record LearningPathStageDto(int Number, string Name, int RequiredCourseCount, int CompletedCourseCount,
    bool Completed, string? SuggestedPrerequisite, IReadOnlyList<LearningPathCourseDto> Courses);
public record LearningPathDto(int Id, int SubjectId, string Name, IReadOnlyList<LearningPathStageDto> Stages, bool Graduated);
public record LearningPathStageUpdateRequest(int RequiredCourseCount);
