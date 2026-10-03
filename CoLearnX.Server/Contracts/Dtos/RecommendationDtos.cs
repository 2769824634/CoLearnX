namespace CoLearnX.Server.Contracts.Dtos;

public record InterestDto(int Id, string Slug, string Name, IReadOnlyList<InterestDto> Children);
public record MemberInterestsDto(IReadOnlyList<int> InterestIds, string? LearningGoals, DateTime? OnboardingCompletedAt, DateTime? OnboardingSkippedAt);
public record UpdateMemberInterestsRequest(IReadOnlyList<int>? InterestIds, string? LearningGoals, bool Skip = false);
public record CourseInterestDto(int Id, string Slug, string Name);
public record SubmitRatingRequest(int Stars, string? Comment);
public record ProgramRatingDto(int EnrollmentId, int CourseId, int Stars, string? Comment, DateTime CreatedAt, DateTime? UpdatedAt);
public record RecommendationItemDto(int CourseId, string Code, string Title, string Level, int CreditCost,
    IReadOnlyList<CourseInterestDto> Interests, double? AverageStars, int RatingCount, double Score);
public record RecommendationResponseDto(string Mode, IReadOnlyList<RecommendationItemDto> Items, int? AnchorCourseId = null);
public record EnrollmentCompletionDto(int EnrollmentId, string Status, int ProgressPercent, DateTime CompletedAt);
