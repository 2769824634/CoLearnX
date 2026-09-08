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
    DateTime CreatedAt);

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
    DateTime? ReviewedAt);

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
    DateTime? ReviewedAt);

public record AdminCourseReviewResultDto(
    AdminCourseDto Course,
    bool AlreadyReviewed);
