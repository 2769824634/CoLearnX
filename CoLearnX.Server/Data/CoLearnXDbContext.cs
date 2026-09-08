using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EF entry point. Shared name: CoLearnXDbContext
public class CoLearnXDbContext(DbContextOptions<CoLearnXDbContext> options) : DbContext(options)
{
    public DbSet<AdminAccount> AdminAccounts => Set<AdminAccount>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
    public DbSet<TrainerProfile> TrainerProfiles => Set<TrainerProfile>();
    public DbSet<CreatorProfile> CreatorProfiles => Set<CreatorProfile>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<UserInterest> UserInterests => Set<UserInterest>();
    public DbSet<RoleRequest> RoleRequests => Set<RoleRequest>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseIntake> CourseIntakes => Set<CourseIntake>();
    public DbSet<CourseSession> CourseSessions => Set<CourseSession>();
    public DbSet<CourseIntakeApplication> CourseIntakeApplications => Set<CourseIntakeApplication>();
    public DbSet<CourseLearningOutcome> CourseLearningOutcomes => Set<CourseLearningOutcome>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<LearningMaterial> LearningMaterials => Set<LearningMaterial>();
    public DbSet<CourseMaterial> CourseMaterials => Set<CourseMaterial>();
    public DbSet<MaterialUsageLog> MaterialUsageLogs => Set<MaterialUsageLog>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<ProgramRating> ProgramRatings => Set<ProgramRating>();
    public DbSet<CreditPackage> CreditPackages => Set<CreditPackage>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
    public DbSet<CertificateTemplate> CertificateTemplates => Set<CertificateTemplate>();
    public DbSet<UserCertificate> UserCertificates => Set<UserCertificate>();
    public DbSet<UserLearningProgress> UserLearningProgress => Set<UserLearningProgress>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminAccount>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).UseCollation("NOCASE");
            e.Property(x => x.PasswordHash).HasMaxLength(128);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.FullName).HasMaxLength(128);
            e.Property(x => x.DisplayName).HasMaxLength(64);
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.Role });
            e.HasOne(x => x.User).WithMany(u => u.Roles).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserPreference>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne(x => x.User).WithOne(u => u.Preference).HasForeignKey<UserPreference>(x => x.UserId);
        });

        modelBuilder.Entity<TrainerProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne(x => x.User).WithOne(u => u.TrainerProfile).HasForeignKey<TrainerProfile>(x => x.UserId);
        });

        modelBuilder.Entity<CreatorProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasOne(x => x.User).WithOne(u => u.CreatorProfile).HasForeignKey<CreatorProfile>(x => x.UserId);
        });

        modelBuilder.Entity<UserInterest>(e =>
        {
            e.HasKey(x => new { x.UserId, x.InterestId });
        });

        modelBuilder.Entity<RoleRequest>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.RequestedRole })
                .IsUnique()
                .HasFilter("\"Status\" = 0");
            e.Property(x => x.DegreeOrResumePath).HasMaxLength(512);
            e.Property(x => x.IdDocumentPath).HasMaxLength(512);
            e.Property(x => x.ReviewNote).HasMaxLength(512);
            e.HasOne(x => x.ReviewedByAdminAccount)
                .WithMany(a => a.ReviewedRoleRequests)
                .HasForeignKey(x => x.ReviewedByAdminAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Course>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasOne(x => x.Trainer).WithMany().HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        });

        CourseIntakeModelConfiguration.Configure(modelBuilder);

        modelBuilder.Entity<WishlistItem>(e =>
        {
            e.HasKey(x => new { x.UserId, x.CourseId });
        });

        modelBuilder.Entity<CourseMaterial>(e =>
        {
            e.HasKey(x => new { x.CourseId, x.LearningMaterialId });
        });

        modelBuilder.Entity<Enrollment>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.CourseId, x.Status });
            e.HasOne(x => x.User).WithMany(u => u.Enrollments).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Course).WithMany(c => c.Enrollments).HasForeignKey(x => x.CourseId);
            e.HasOne(x => x.CourseSession).WithMany(s => s.Enrollments).HasForeignKey(x => x.CourseSessionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProgramRating>(e =>
        {
            e.HasKey(x => x.EnrollmentId);
            e.HasOne(x => x.Enrollment).WithOne(en => en.Rating).HasForeignKey<ProgramRating>(x => x.EnrollmentId);
        });

        modelBuilder.Entity<CreditTransaction>(e =>
        {
            e.HasOne(x => x.User).WithMany(u => u.CreditTransactions).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<LearningMaterial>(e =>
        {
            e.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs", table => table.HasCheckConstraint(
                "CK_AuditLogs_ExactlyOneActor",
                "(AdminAccountId IS NOT NULL AND UserId IS NULL) OR (AdminAccountId IS NULL AND UserId IS NOT NULL)"));
            e.HasIndex(x => x.CreatedAt);
            e.Property(x => x.Action).HasMaxLength(128);
            e.Property(x => x.EntityType).HasMaxLength(128);
            e.Property(x => x.EntityId).HasMaxLength(128);
            e.Property(x => x.Result).HasMaxLength(32);
            e.Property(x => x.Reason).HasMaxLength(512);
            e.HasOne(x => x.AdminAccount)
                .WithMany(a => a.AuditLogs)
                .HasForeignKey(x => x.AdminAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
