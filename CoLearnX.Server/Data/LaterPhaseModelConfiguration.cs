using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

internal static class LaterPhaseModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourseMaterialVersion>(entity =>
        {
            entity.ToTable("CourseMaterialVersions", table =>
                table.HasCheckConstraint("CK_CourseMaterialVersions_Status", "Status BETWEEN 0 AND 2"));
            entity.HasIndex(item => new { item.LearningMaterialId, item.VersionNumber }).IsUnique();
            entity.Property(item => item.FilePath).HasMaxLength(512);
            entity.Property(item => item.Format).HasMaxLength(32);
            entity.Property(item => item.ReviewReason).HasMaxLength(512);
            entity.HasOne(item => item.LearningMaterial).WithMany().HasForeignKey(item => item.LearningMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReviewedByAdminAccount).WithMany().HasForeignKey(item => item.ReviewedByAdminAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CourseIntakeMaterial>(entity =>
        {
            entity.HasKey(item => new { item.CourseIntakeId, item.CourseMaterialVersionId });
            entity.HasOne(item => item.CourseIntake).WithMany().HasForeignKey(item => item.CourseIntakeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CourseMaterialVersion).WithMany().HasForeignKey(item => item.CourseMaterialVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SessionRecording>(entity =>
        {
            entity.HasIndex(item => new { item.CourseSessionId, item.RecordingUrl }).IsUnique();
            entity.Property(item => item.Title).HasMaxLength(160);
            entity.Property(item => item.RecordingUrl).HasMaxLength(1024);
            entity.HasOne(item => item.CourseSession).WithMany().HasForeignKey(item => item.CourseSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasIndex(item => new { item.CourseSessionId, item.UserId }).IsUnique();
            entity.ToTable("AttendanceRecords", table =>
                table.HasCheckConstraint("CK_AttendanceRecords_Status", "Status BETWEEN 0 AND 2"));
        });

        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.ToTable("Assessments", table => table.HasCheckConstraint(
                "CK_Assessments_Scores", "MaxScore > 0 AND PassScore >= 0 AND PassScore <= MaxScore"));
            entity.Property(item => item.Title).HasMaxLength(160);
            entity.HasOne(item => item.CourseIntake).WithMany().HasForeignKey(item => item.CourseIntakeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssessmentResult>(entity =>
        {
            entity.HasIndex(item => new { item.AssessmentId, item.EnrollmentId }).IsUnique();
            entity.Property(item => item.Feedback).HasMaxLength(1000);
            entity.HasOne(item => item.Assessment).WithMany(item => item.Results).HasForeignKey(item => item.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Enrollment).WithMany().HasForeignKey(item => item.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CertificateRequest>(entity =>
        {
            entity.ToTable("CertificateRequests", table =>
                table.HasCheckConstraint("CK_CertificateRequests_Status", "Status BETWEEN 0 AND 4"));
            entity.HasIndex(item => item.EnrollmentId).IsUnique();
            entity.Property(item => item.TrainerReviewReason).HasMaxLength(512);
            entity.Property(item => item.AdminReviewReason).HasMaxLength(512);
            entity.HasOne(item => item.Enrollment).WithMany().HasForeignKey(item => item.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AdminReviewedByAccount).WithMany().HasForeignKey(item => item.AdminReviewedByAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.UserCertificate).WithMany().HasForeignKey(item => item.UserCertificateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CreditTransaction>(entity =>
        {
            entity.HasIndex(item => item.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            entity.HasOne<Dispute>().WithMany().HasForeignKey(item => item.RelatedDisputeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AdminAccount>().WithMany().HasForeignKey(item => item.AdminAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.ToTable("Disputes", table =>
                table.HasCheckConstraint("CK_Disputes_Status", "Status BETWEEN 0 AND 2"));
            entity.HasIndex(item => item.ResolutionKey).IsUnique().HasFilter("\"ResolutionKey\" IS NOT NULL");
            entity.Property(item => item.Reason).HasMaxLength(1000);
            entity.Property(item => item.ResolutionNote).HasMaxLength(512);
            entity.HasOne<User>().WithMany().HasForeignKey(item => item.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Enrollment).WithMany().HasForeignKey(item => item.EnrollmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AdminAccount>().WithMany().HasForeignKey(item => item.HandledByAdminId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
