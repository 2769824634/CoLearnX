using System.ComponentModel.DataAnnotations;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Contracts.Dtos;

// Auth request/response DTOs. Shared: LoginRequest, AuthResponse, UserMeDto
public record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(PasswordRules.MinLength), MaxLength(PasswordRules.MaxLength)]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Hint)]
    string Password,
    [Required, MinLength(2), MaxLength(80)] string FullName,
    [MaxLength(80)] string? DisplayName);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    [Required] string ActiveRole);

public record AvailableRolesRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AvailableRolesDto(IReadOnlyList<string> Roles);

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
    IReadOnlyDictionary<string, bool> IdentityVisibility,
    string? LearningGoals = null,
    string? Specialisations = null,
    string? TrainerHeadline = null,
    string? ExpertiseTags = null,
    string? CreatorHeadline = null,
    bool EmailNotifications = true);

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
    string? TrainerHeadline,
    string? ExpertiseTags = null,
    string? CreatorHeadline = null);

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
    int SeatsLeft,
    int CourseIntakeId = 0,
    string? MeetingLink = null,
    string? PhysicalAddress = null,
    int PhysicalCapacity = 0,
    DateTime? PhysicalBookingDeadline = null);

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
    int Version,
    int CourseId = 0,
    string? CourseCode = null,
    string? CourseTitle = null);

public record CreatorMaterialUsageDto(
    int Id,
    int MaterialId,
    string MaterialTitle,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    int TrainerId,
    string TrainerName,
    DateTime UsedAt);

public record StorageStatusDto(string Provider, bool CloudLinks, string? Container);

public record MaterialCloudLinkDto(string Url, DateTime ExpiresAtUtc);

public record CreateRoleRequest([Required] string RequestedRole);

public record RoleRequestDto(
    int Id,
    string RequestedRole,
    string Status,
    string? ReviewNote,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    bool HasResume = false,
    bool HasIdDocument = false,
    string? Statement = null);

public record ApiError(string Code, string Message,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);
