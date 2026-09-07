using System.ComponentModel.DataAnnotations;
using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Contracts.Dtos;

// Auth request/response DTOs.
public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Required] string FullName,
    string? DisplayName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    [Required] string ActiveRole);

public record SwitchRoleRequest([Required] string ActiveRole);

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    UserMeDto User);

public record UserMeDto(
    int Id,
    string Email,
    string FullName,
    string DisplayName,
    string? Phone,
    string? Bio,
    int CreditBalance,
    string ActiveRole,
    IReadOnlyList<string> Roles,
    IReadOnlyDictionary<string, bool> IdentityVisibility);

public record UpdateProfileRequest(
    string? FullName,
    string? DisplayName,
    string? Phone,
    string? Bio,
    string? LearningGoals,
    bool? EmailNotifications,
    bool? DarkMode,
    IReadOnlyDictionary<string, bool>? IdentityVisibility,
    string? Specialisations,
    string? TrainerHeadline);

public record CourseListItemDto(
    int Id,
    string Code,
    string Title,
    string TrainerName,
    int CreditCost,
    string Level,
    string Category,
    bool IsFeatured,
    bool InWishlist);

public record CourseSessionDto(
    int Id,
    string Label,
    DateTime StartsAt,
    DateTime EndsAt,
    int Capacity,
    int SeatsLeft);

public record CourseDetailDto(
    int Id,
    string Code,
    string Title,
    string? Description,
    string TrainerName,
    int CreditCost,
    string Level,
    string Category,
    string Status,
    IReadOnlyList<string> LearningOutcomes,
    IReadOnlyList<CourseSessionDto> Sessions,
    bool InWishlist,
    bool AlreadyEnrolled);

public record WishlistResultDto(int CourseId, bool InWishlist);

public record EnrolRequest(
    [Required] int CourseId,
    [Required] int CourseSessionId);

public record EnrolResultDto(
    int EnrollmentId,
    int CreditsSpent,
    int BalanceAfter,
    string CourseCode,
    string CourseTitle);

public record EnrollmentDto(
    int Id,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    string TrainerName,
    string Status,
    int ProgressPercent,
    int CourseSessionId);

public record CreditPackageDto(
    int Id,
    int PayAudCents,
    decimal PayAud,
    int Credits,
    string? Note,
    bool IsBestValue);

public record CreditLedgerItemDto(
    int Id,
    DateTime CreatedAt,
    string Type,
    string Description,
    int Delta,
    int BalanceAfter);

public record TopUpRequest([Required] int CreditPackageId, string? ProviderReference);

public record CertificateDto(
    int Id,
    string StageName,
    int StageNumber,
    string Title,
    DateTime AwardedAt,
    string VerificationCode);

public record MaterialDto(
    int Id,
    string Title,
    string Format,
    string Category,
    string Status,
    string CreatorName,
    int Version);

public record ApiError(string Code, string Message);
