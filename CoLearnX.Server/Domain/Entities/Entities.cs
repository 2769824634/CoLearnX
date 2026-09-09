using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Domain.Entities;

// Account root. Shared name: User
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public int CreditBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserPreference? Preference { get; set; }
    public TrainerProfile? TrainerProfile { get; set; }
    public CreatorProfile? CreatorProfile { get; set; }
    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<CreditTransaction> CreditTransactions { get; set; } = new List<CreditTransaction>();
    public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    public ICollection<RoleRequest> RoleRequests { get; set; } = new List<RoleRequest>();
    public ICollection<UserCertificate> Certificates { get; set; } = new List<UserCertificate>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

// Multi-role link. Shared: UserRole, AppRole
public class UserRole
{
    public int UserId { get; set; }
    public AppRole Role { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

public class UserPreference
{
    public int UserId { get; set; }
    public bool EmailNotifications { get; set; } = true;
    public bool DarkMode { get; set; }
    public string? LearningGoals { get; set; }

    public User User { get; set; } = null!;
}

public class TrainerProfile
{
    public int UserId { get; set; }
    public string? Specialisations { get; set; }
    public string? Headline { get; set; }
    public bool ShowProgramsTaught { get; set; } = true;

    public User User { get; set; } = null!;
}

public class CreatorProfile
{
    public int UserId { get; set; }
    public string? ExpertiseTags { get; set; }
    public string? Headline { get; set; }

    public User User { get; set; } = null!;
}

public class Interest
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public class CourseLevel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}

public class LearningPath
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}

public class UserInterest
{
    public int UserId { get; set; }
    public int InterestId { get; set; }

    public User User { get; set; } = null!;
    public Interest Interest { get; set; } = null!;
}

public class RoleRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppRole RequestedRole { get; set; }
    public RoleRequestStatus Status { get; set; } = RoleRequestStatus.Pending;
    public string? DegreeOrResumePath { get; set; }
    public string? IdDocumentPath { get; set; }
    public string? ReviewNote { get; set; }
    public int? ReviewedByAdminAccountId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public User User { get; set; } = null!;
    public AdminAccount? ReviewedByAdminAccount { get; set; }
}

// Training program. Shared: Course, CourseSession, Enrollment
public class Course
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TrainerId { get; set; }
    // The Creator owns Intake confirmation. TrainerId remains the delivery owner.
    public int CreatorId { get; set; }
    public int CreditCost { get; set; }
    public int? CourseLevelId { get; set; }
    public int? LearningPathId { get; set; }
    public string Level { get; set; } = "Beginner";
    public string Category { get; set; } = string.Empty;
    public CourseStatus Status { get; set; } = CourseStatus.Draft;
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Trainer { get; set; } = null!;
    public User Creator { get; set; } = null!;
    public CourseLevel? CourseLevel { get; set; }
    public LearningPath? LearningPath { get; set; }
    public ICollection<CourseIntake> Intakes { get; set; } = new List<CourseIntake>();
    public ICollection<CourseLearningOutcome> LearningOutcomes { get; set; } = new List<CourseLearningOutcome>();
    public ICollection<CourseMaterial> CourseMaterials { get; set; } = new List<CourseMaterial>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

public class CourseLearningOutcome
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int SortOrder { get; set; }
    public string Text { get; set; } = string.Empty;

    public Course Course { get; set; } = null!;
}

public class WishlistItem
{
    public int UserId { get; set; }
    public int CourseId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Course Course { get; set; } = null!;
}

// Creator content. Shared: LearningMaterial
public class LearningMaterial
{
    public int Id { get; set; }
    public int CreatorId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Format { get; set; } = "PDF";
    public string Category { get; set; } = string.Empty;
    public MaterialStatus Status { get; set; } = MaterialStatus.Draft;
    public string? RejectionReason { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Creator { get; set; } = null!;
    public ICollection<CourseMaterial> CourseMaterials { get; set; } = new List<CourseMaterial>();
    public ICollection<MaterialUsageLog> UsageLogs { get; set; } = new List<MaterialUsageLog>();
}

public class CourseMaterial
{
    public int CourseId { get; set; }
    public int LearningMaterialId { get; set; }
    public DateTime AttachedAt { get; set; } = DateTime.UtcNow;

    public Course Course { get; set; } = null!;
    public LearningMaterial LearningMaterial { get; set; } = null!;
}

public class MaterialUsageLog
{
    public int Id { get; set; }
    public int LearningMaterialId { get; set; }
    public int CourseId { get; set; }
    public int TrainerId { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    public int? RoyaltyCredits { get; set; }

    public LearningMaterial LearningMaterial { get; set; } = null!;
}

// Member enrol in a session. Shared: Enrollment
public class Enrollment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CourseId { get; set; }
    public int CourseSessionId { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public int ProgressPercent { get; set; }
    public int CreditsSpent { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public Course Course { get; set; } = null!;
    public CourseSession CourseSession { get; set; } = null!;
    public ProgramRating? Rating { get; set; }
}

public class AttendanceRecord
{
    public int Id { get; set; }
    public int CourseSessionId { get; set; }
    public int UserId { get; set; }
    public AttendanceStatus Status { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public int RecordedByTrainerId { get; set; }

    public CourseSession CourseSession { get; set; } = null!;
}

public class ProgramRating
{
    public int EnrollmentId { get; set; }
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public bool AllowTrainerView { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Enrollment Enrollment { get; set; } = null!;
}

// Fixed PayPal top-up tiers. Shared: CreditPackage
public class CreditPackage
{
    public int Id { get; set; }
    public int PayAudCents { get; set; }
    public int Credits { get; set; }
    public string? Note { get; set; }
    public bool IsBestValue { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class PaymentTransaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CreditPackageId { get; set; }
    public string Provider { get; set; } = "PayPal";
    public string? ProviderReference { get; set; }
    public int AmountAudCents { get; set; }
    public int CreditsGranted { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public CreditPackage CreditPackage { get; set; } = null!;
}

// Every credit change must write a row. Shared: CreditTransaction
public class CreditTransaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public CreditTransactionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Delta { get; set; }
    public int BalanceAfter { get; set; }
    public int? RelatedEnrollmentId { get; set; }
    public int? RelatedPaymentId { get; set; }
    public int? RelatedDisputeId { get; set; }
    public int? AdminAccountId { get; set; }
    public Guid? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}

public class CertificateTemplate
{
    public int Id { get; set; }
    public string StageName { get; set; } = string.Empty;
    public int StageNumber { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class UserCertificate
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CertificateTemplateId { get; set; }
    public int? CourseId { get; set; }
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
    public bool AdminApproved { get; set; }
    public int? RequestedByTrainerId { get; set; }

    public User User { get; set; } = null!;
    public CertificateTemplate CertificateTemplate { get; set; } = null!;
}

public class UserLearningProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int StageNumber { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class Dispute
{
    public int Id { get; set; }
    public int RaisedByUserId { get; set; }
    public int? EnrollmentId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public int? HandledByAdminId { get; set; }
    public string? ResolutionNote { get; set; }
    public int? RefundCredits { get; set; }
    public Guid? ResolutionKey { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public Enrollment? Enrollment { get; set; }
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
