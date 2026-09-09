using System.ComponentModel.DataAnnotations;

namespace CoLearnX.Server.Contracts.Dtos;

public record CreateMaterialVersionRequest(
    [Required, MaxLength(160)] string Title,
    [MaxLength(1000)] string? Description,
    [Required, MaxLength(512)] string FilePath,
    [Required, MaxLength(32)] string Format,
    [Required, MaxLength(80)] string Category);

public record MaterialVersionDto(
    int VersionId,
    int LearningMaterialId,
    string Title,
    string? Description,
    string FilePath,
    string Format,
    string Category,
    int VersionNumber,
    string Status,
    string CreatorName,
    string? ReviewReason,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    bool AlreadyReviewed = false);

public record AttachIntakeMaterialRequest([Range(1, int.MaxValue)] int MaterialVersionId);

public record IntakeMaterialDto(
    int MaterialVersionId,
    int LearningMaterialId,
    string Title,
    string Format,
    string FilePath,
    DateTime AttachedAt);

public record CreateSessionRecordingRequest(
    [Required, MaxLength(160)] string Title,
    [Required, MaxLength(1024)] string RecordingUrl);

public record SessionRecordingDto(int Id, int CourseSessionId, string Title, string RecordingUrl, DateTime CreatedAt);

public record AttendanceItemRequest(
    [Range(1, int.MaxValue)] int EnrollmentId,
    [Required] string Status);

public record SaveAttendanceRequest(
    [Required, MinLength(1)] IReadOnlyList<AttendanceItemRequest> Records);

public record AttendanceItemDto(int EnrollmentId, int UserId, string LearnerName, string Status, DateTime RecordedAt);

public record TrainerLearnerDto(
    int EnrollmentId,
    int CourseSessionId,
    int UserId,
    string FullName,
    string Email,
    string EnrollmentStatus,
    int ProgressPercent,
    int AttendanceRate,
    int AssessmentsGraded,
    int AssessmentsPassed,
    string? AttendanceStatus,
    string? CertificateRequestStatus);

public record CreateAssessmentRequest(
    [Required, MaxLength(160)] string Title,
    [Range(1, 100000)] int MaxScore,
    [Range(0, 100000)] int PassScore,
    DateTime? DueAt);

public record GradeAssessmentRequest(
    [Range(0, 100000)] int Score,
    [MaxLength(1000)] string? Feedback);

public record AssessmentResultDto(
    int EnrollmentId,
    int UserId,
    string LearnerName,
    int Score,
    bool Passed,
    string? Feedback,
    DateTime GradedAt);

public record AssessmentDto(
    int Id,
    int CourseIntakeId,
    string Title,
    int MaxScore,
    int PassScore,
    DateTime? DueAt,
    DateTime CreatedAt,
    IReadOnlyList<AssessmentResultDto> Results);

public record SubmitCertificateRequest([Range(1, int.MaxValue)] int EnrollmentId);

public record WorkflowReviewRequest(
    [Required] string Decision,
    [MaxLength(512)] string? Reason);

public record CertificateRequestDto(
    int Id,
    int EnrollmentId,
    int UserId,
    string LearnerName,
    string CourseCode,
    string CourseTitle,
    int CourseIntakeId,
    string Status,
    DateTime SubmittedAt,
    string? TrainerReviewReason,
    string? AdminReviewReason,
    int? CertificateId);

public record AdminCreditLedgerItemDto(
    int Id,
    DateTime CreatedAt,
    int UserId,
    string UserName,
    string UserEmail,
    string Type,
    string Description,
    int Delta,
    int BalanceAfter,
    int? RelatedEnrollmentId,
    int? RelatedDisputeId,
    int? AdminAccountId);

public record AdminCreditAdjustmentRequest(
    [Range(1, int.MaxValue)] int UserId,
    [Range(-10000, 10000)] int Delta,
    [Required, MaxLength(512)] string Reason,
    Guid IdempotencyKey);

public record AdminCreditMutationDto(
    int TransactionId,
    int UserId,
    int Delta,
    int BalanceAfter,
    string Type,
    bool Replayed);

public record CreateDisputeRequest(
    [Range(1, int.MaxValue)] int EnrollmentId,
    [Required, MaxLength(1000)] string Reason);

public record AdminDisputeReviewRequest(
    [Required] string Decision,
    [Range(1, 10000)] int? RefundCredits,
    [Required, MaxLength(512)] string Reason,
    Guid IdempotencyKey);

public record DisputeDto(
    int Id,
    int RaisedByUserId,
    string UserName,
    string UserEmail,
    int EnrollmentId,
    string CourseCode,
    string CourseTitle,
    string Reason,
    string Status,
    int CreditsSpent,
    int? RefundCredits,
    string? ResolutionNote,
    DateTime CreatedAt,
    DateTime? ResolvedAt,
    int? BalanceAfter = null,
    bool Replayed = false);
