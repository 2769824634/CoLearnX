using System.ComponentModel.DataAnnotations;

namespace CoLearnX.Server.Contracts.Dtos;

public record AdminLoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AdminAccountDto(
    int Id,
    string Email);

public record AdminAuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    AdminAccountDto Admin);

public record AuditLogDto(
    int Id,
    int? AdminAccountId,
    int? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    string Result,
    string? Reason,
    DateTime CreatedAt,
    int? StaffAccountId = null);

public record AuditLogFilterOptionsDto(IReadOnlyList<string> EntityTypes, IReadOnlyList<string> Results);

public record AdminReviewRequest(
    [Required] string Decision,
    [MaxLength(512)] string? Reason);

public record AdminRoleRequestDto(
    int Id,
    int UserId,
    string UserEmail,
    string UserFullName,
    string RequestedRole,
    string Status,
    string? DegreeOrResumePath,
    string? IdDocumentPath,
    string? ReviewNote,
    int? ReviewedByAdminAccountId,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ApplicantStatement = null);

public record AdminRoleRequestReviewResultDto(
    AdminRoleRequestDto RoleRequest,
    bool AlreadyReviewed);

public record AdminCourseDto(
    int Id,
    string Code,
    string Title,
    string? Description,
    int SubmittedByUserId,
    string SubmittedByEmail,
    string SubmittedByName,
    int CreditCost,
    string Level,
    string Category,
    string Status,
    string? ReviewReason,
    int? ReviewedByAdminAccountId,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    IReadOnlyList<string>? LearningOutcomes = null,
    string? LearningPath = null,
    string? CreatorName = null,
    IReadOnlyList<CourseInterestDto>? Interests = null,
    int MaterialVersionCount = 0,
    IReadOnlyDictionary<string, int>? MaterialVersionStatusCounts = null,
    IReadOnlyList<AdminCourseMaterialVersionDto>? MaterialVersions = null,
    IReadOnlyList<AdminCourseTrainerDto>? Trainers = null);

public record AdminCourseTrainerDto(int TrainerId, string FullName, string Status);

public record AdminCourseMaterialVersionDto(
    int VersionId,
    int LearningMaterialId,
    string Title,
    string Format,
    int VersionNumber,
    string Status);

public record AdminCourseReviewResultDto(
    AdminCourseDto Course,
    bool AlreadyReviewed);

public record AdminUserSummaryDto(int Id, string FullName, string Email, int CreditBalance,
    int HeldCredits, bool IsActive, IReadOnlyList<string> Roles);

public record AdminPendingCountsDto(int RoleRequests, int Courses, int MaterialVersions, int Certificates, int Disputes);

public sealed class ProvisionStaffRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(2), MaxLength(128)]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(32)]
    public string? Phone { get; init; }

    [Required]
    public IFormFile? Document { get; init; }
}

public record SetStaffActiveRequest(bool IsActive);

public record StaffAccountDto(
    int Id,
    string Email,
    string FullName,
    string DisplayName,
    string? Phone,
    int StaffNumber,
    bool IsActive,
    int UserId,
    DateTime CreatedAt);

public record StaffProvisioningResultDto(StaffAccountDto Staff, string InitialPassword);

public record StaffLoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record StaffAuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    StaffAccountDto Staff);
