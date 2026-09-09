using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Domain.Entities;

public sealed class CourseMaterialVersion
{
    public int Id { get; set; }
    public int LearningMaterialId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string FilePath { get; set; } = string.Empty;
    public string Format { get; set; } = "PDF";
    public MaterialVersionStatus Status { get; set; } = MaterialVersionStatus.PendingApproval;
    public string? ReviewReason { get; set; }
    public int? ReviewedByAdminAccountId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public LearningMaterial LearningMaterial { get; set; } = null!;
    public AdminAccount? ReviewedByAdminAccount { get; set; }
}

public sealed class CourseIntakeMaterial
{
    public int CourseIntakeId { get; set; }
    public int CourseMaterialVersionId { get; set; }
    public int AttachedByTrainerId { get; set; }
    public DateTime AttachedAt { get; set; } = DateTime.UtcNow;

    public CourseIntake CourseIntake { get; set; } = null!;
    public CourseMaterialVersion CourseMaterialVersion { get; set; } = null!;
}

public sealed class SessionRecording
{
    public int Id { get; set; }
    public int CourseSessionId { get; set; }
    public int AddedByTrainerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RecordingUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CourseSession CourseSession { get; set; } = null!;
}

public sealed class Assessment
{
    public int Id { get; set; }
    public int CourseIntakeId { get; set; }
    public int CreatedByTrainerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int MaxScore { get; set; }
    public int PassScore { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CourseIntake CourseIntake { get; set; } = null!;
    public ICollection<AssessmentResult> Results { get; set; } = new List<AssessmentResult>();
}

public sealed class AssessmentResult
{
    public int Id { get; set; }
    public int AssessmentId { get; set; }
    public int EnrollmentId { get; set; }
    public int Score { get; set; }
    public string? Feedback { get; set; }
    public int GradedByTrainerId { get; set; }
    public DateTime GradedAt { get; set; } = DateTime.UtcNow;

    public Assessment Assessment { get; set; } = null!;
    public Enrollment Enrollment { get; set; } = null!;
}

public sealed class CertificateRequest
{
    public int Id { get; set; }
    public int EnrollmentId { get; set; }
    public CertificateRequestStatus Status { get; set; } = CertificateRequestStatus.Submitted;
    public int? TrainerReviewedByUserId { get; set; }
    public DateTime? TrainerReviewedAt { get; set; }
    public string? TrainerReviewReason { get; set; }
    public int? AdminReviewedByAccountId { get; set; }
    public DateTime? AdminReviewedAt { get; set; }
    public string? AdminReviewReason { get; set; }
    public int? UserCertificateId { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Enrollment Enrollment { get; set; } = null!;
    public AdminAccount? AdminReviewedByAccount { get; set; }
    public UserCertificate? UserCertificate { get; set; }
}
