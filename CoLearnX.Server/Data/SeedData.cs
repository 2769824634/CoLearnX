using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// Demo users, courses, packages.
public static class SeedData
{
    public const string DemoPassword = "Password123!";

    public static async Task InitializeAsync(CoLearnXDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync()) return;

        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);

        var member = new User
        {
            Email = "huang.yousheng@colearnx.com",
            PasswordHash = hash,
            FullName = "Huang Yousheng",
            DisplayName = "Yousheng",
            Phone = "12345678",
            Bio = "The sole disciple of the Way of Mercilessness",
            CreditBalance = 120,
        };
        var trainer = new User
        {
            Email = "jane.smith@colearnx.com",
            PasswordHash = hash,
            FullName = "Jane Smith",
            DisplayName = "Jane",
            Phone = "87654321",
            Bio = "Workshop facilitator · design thinking",
            CreditBalance = 0,
        };
        var creator = new User
        {
            Email = "alex.lee@colearnx.com",
            PasswordHash = hash,
            FullName = "Alex Lee",
            DisplayName = "Alex",
            Phone = "11223344",
            Bio = "Learning materials creator",
            CreditBalance = 40,
        };
        var admin = new User
        {
            Email = "desmond.tan@colearnx.com",
            PasswordHash = hash,
            FullName = "Desmond Tan",
            DisplayName = "Desmond",
            Phone = "99887766",
            Bio = "Platform administrator",
            CreditBalance = 0,
        };

        db.Users.AddRange(member, trainer, creator, admin);
        await db.SaveChangesAsync();

        db.UserRoles.AddRange(
            new UserRole { UserId = member.Id, Role = AppRole.Member, IsVisible = true },
            new UserRole { UserId = member.Id, Role = AppRole.Trainer, IsVisible = true },
            new UserRole { UserId = trainer.Id, Role = AppRole.Trainer, IsVisible = true },
            new UserRole { UserId = trainer.Id, Role = AppRole.Member, IsVisible = true },
            new UserRole { UserId = creator.Id, Role = AppRole.Creator, IsVisible = true },
            new UserRole { UserId = creator.Id, Role = AppRole.Member, IsVisible = true },
            new UserRole { UserId = admin.Id, Role = AppRole.Admin, IsVisible = true }
        );

        db.UserPreferences.AddRange(
            new UserPreference { UserId = member.Id, LearningGoals = "Career switch · UI/UX & Cybersecurity" },
            new UserPreference { UserId = trainer.Id },
            new UserPreference { UserId = creator.Id },
            new UserPreference { UserId = admin.Id }
        );

        db.TrainerProfiles.Add(new TrainerProfile
        {
            UserId = member.Id,
            Specialisations = "UI/UX Design, Wireframing, Usability",
            Headline = "Workshop facilitator · design thinking",
        });
        db.TrainerProfiles.Add(new TrainerProfile
        {
            UserId = trainer.Id,
            Specialisations = "UI/UX Design, Cybersecurity awareness",
            Headline = "Senior Trainer",
        });
        db.CreatorProfiles.Add(new CreatorProfile
        {
            UserId = creator.Id,
            ExpertiseTags = "Cybersecurity, Cloud",
            Headline = "Content creator",
        });

        db.CreditPackages.AddRange(
            new CreditPackage { PayAudCents = 2000, Credits = 20, Note = "1:1", SortOrder = 1 },
            new CreditPackage { PayAudCents = 5000, Credits = 55, Note = "+5 bonus", SortOrder = 2 },
            new CreditPackage { PayAudCents = 10000, Credits = 115, Note = "+15", SortOrder = 3 },
            new CreditPackage { PayAudCents = 20000, Credits = 240, Note = "+40", IsBestValue = true, SortOrder = 4 },
            new CreditPackage { PayAudCents = 50000, Credits = 625, Note = "+125", SortOrder = 5 },
            new CreditPackage { PayAudCents = 100000, Credits = 1300, Note = "+300", SortOrder = 6 }
        );

        db.CertificateTemplates.AddRange(
            new CertificateTemplate { StageNumber = 1, StageName = "Foundation", Title = "Foundation Stage Certificate" },
            new CertificateTemplate { StageNumber = 2, StageName = "Intermediate", Title = "Intermediate Stage Certificate" },
            new CertificateTemplate { StageNumber = 3, StageName = "Advanced", Title = "Advanced Stage Certificate" },
            new CertificateTemplate { StageNumber = 4, StageName = "Graduation", Title = "Graduation Stage Certificate" }
        );

        var c1 = new Course
        {
            Code = "INFT 2051",
            Title = "UI/UX Design Fundamentals",
            Description = "Core UI/UX concepts and hands-on practice.",
            TrainerId = trainer.Id,
            CreditCost = 30,
            Level = "Beginner",
            Category = "Design",
            Status = CourseStatus.Published,
            IsFeatured = true,
        };
        var c2 = new Course
        {
            Code = "INFT 3030",
            Title = "Cybersecurity Essentials",
            Description = "Essential cybersecurity practices.",
            TrainerId = trainer.Id,
            CreditCost = 25,
            Level = "Intermediate",
            Category = "Programming",
            Status = CourseStatus.Published,
            IsFeatured = true,
        };
        var c3 = new Course
        {
            Code = "INFT 2002",
            Title = "Frontend React Bootcamp",
            Description = "Build modern React applications.",
            TrainerId = trainer.Id,
            CreditCost = 20,
            Level = "Beginner",
            Category = "Programming",
            Status = CourseStatus.Published,
            IsFeatured = true,
        };
        var c4 = new Course
        {
            Code = "INFT 4010",
            Title = "UX Research Methods",
            Description = "Research methods for UX practitioners.",
            TrainerId = trainer.Id,
            CreditCost = 35,
            Level = "Intermediate",
            Category = "Design",
            Status = CourseStatus.Published,
        };
        db.Courses.AddRange(c1, c2, c3, c4);
        await db.SaveChangesAsync();

        var s1 = new CourseSession
        {
            CourseId = c1.Id,
            Label = "Session 1",
            StartsAt = new DateTime(2026, 5, 20, 9, 30, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 5, 20, 18, 30, 0, DateTimeKind.Utc),
            Capacity = 20,
            SeatsTaken = 8,
        };
        var s2 = new CourseSession
        {
            CourseId = c1.Id,
            Label = "Session 2",
            StartsAt = new DateTime(2026, 6, 10, 9, 30, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 6, 10, 18, 30, 0, DateTimeKind.Utc),
            Capacity = 20,
            SeatsTaken = 12,
        };
        var s3 = new CourseSession
        {
            CourseId = c2.Id,
            Label = "Session 1",
            StartsAt = new DateTime(2026, 5, 22, 14, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 5, 22, 18, 0, 0, DateTimeKind.Utc),
            Capacity = 25,
            SeatsTaken = 10,
        };
        var s4 = new CourseSession
        {
            CourseId = c3.Id,
            Label = "Session 1",
            StartsAt = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 6, 15, 16, 0, 0, DateTimeKind.Utc),
            Capacity = 30,
            SeatsTaken = 5,
        };
        var s5 = new CourseSession
        {
            CourseId = c4.Id,
            Label = "Session 1",
            StartsAt = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 7, 1, 17, 0, 0, DateTimeKind.Utc),
            Capacity = 18,
            SeatsTaken = 3,
        };
        db.CourseSessions.AddRange(s1, s2, s3, s4, s5);
        await db.SaveChangesAsync();

        db.CourseLearningOutcomes.AddRange(
            new CourseLearningOutcome { CourseId = c1.Id, SortOrder = 1, Text = "Understand core concepts and principles" },
            new CourseLearningOutcome { CourseId = c1.Id, SortOrder = 2, Text = "Apply practical techniques hands-on" },
            new CourseLearningOutcome { CourseId = c1.Id, SortOrder = 3, Text = "Conduct basic research and analysis" },
            new CourseLearningOutcome { CourseId = c1.Id, SortOrder = 4, Text = "Design user-friendly solutions" }
        );

        db.WishlistItems.AddRange(
            new WishlistItem { UserId = member.Id, CourseId = c2.Id },
            new WishlistItem { UserId = member.Id, CourseId = c4.Id }
        );

        db.Enrollments.AddRange(
            new Enrollment
            {
                UserId = member.Id,
                CourseId = c1.Id,
                CourseSessionId = s1.Id,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 65,
                CreditsSpent = 30,
            },
            new Enrollment
            {
                UserId = member.Id,
                CourseId = c2.Id,
                CourseSessionId = s3.Id,
                Status = EnrollmentStatus.Active,
                ProgressPercent = 12,
                CreditsSpent = 25,
            },
            new Enrollment
            {
                UserId = member.Id,
                CourseId = c3.Id,
                CourseSessionId = s4.Id,
                Status = EnrollmentStatus.Completed,
                ProgressPercent = 100,
                CreditsSpent = 20,
                CompletedAt = new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc),
            }
        );

        db.CreditTransactions.AddRange(
            new CreditTransaction
            {
                UserId = member.Id,
                Type = CreditTransactionType.TopUp,
                Description = "PayPal package +120",
                Delta = 120,
                BalanceAfter = 120,
                CreatedAt = new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc),
            },
            new CreditTransaction
            {
                UserId = member.Id,
                Type = CreditTransactionType.Enrolment,
                Description = "INFT 3030 — Cybersecurity Essentials",
                Delta = -25,
                BalanceAfter = 0,
                CreatedAt = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            },
            new CreditTransaction
            {
                UserId = member.Id,
                Type = CreditTransactionType.Enrolment,
                Description = "INFT 2051 — UI/UX Design Fundamentals",
                Delta = -30,
                BalanceAfter = 25,
                CreatedAt = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
            }
        );

        var material = new LearningMaterial
        {
            CreatorId = creator.Id,
            Title = "UI/UX Basics",
            Description = "Introductory PDF for UI/UX",
            FilePath = "materials/uiux-basics.pdf",
            Format = "PDF",
            Category = "Design",
            Status = MaterialStatus.Approved,
        };
        db.LearningMaterials.Add(material);
        await db.SaveChangesAsync();

        db.CourseMaterials.Add(new CourseMaterial { CourseId = c1.Id, LearningMaterialId = material.Id });
        db.MaterialUsageLogs.Add(new MaterialUsageLog
        {
            LearningMaterialId = material.Id,
            CourseId = c1.Id,
            TrainerId = trainer.Id,
            RoyaltyCredits = 2,
        });

        var templates = await db.CertificateTemplates.OrderBy(t => t.StageNumber).ToListAsync();
        foreach (var t in templates)
        {
            db.UserCertificates.Add(new UserCertificate
            {
                UserId = member.Id,
                CertificateTemplateId = t.Id,
                CourseId = c3.Id,
                VerificationCode = $"CLX-{t.StageNumber}-ABC{t.StageNumber}",
                AwardedAt = new DateTime(2026, 3 + t.StageNumber, 12, 0, 0, 0, DateTimeKind.Utc),
                AdminApproved = true,
                RequestedByTrainerId = trainer.Id,
            });
            db.UserLearningProgress.Add(new UserLearningProgress
            {
                UserId = member.Id,
                StageNumber = t.StageNumber,
                StageName = t.StageName,
                Completed = true,
                CompletedAt = new DateTime(2026, 3 + t.StageNumber, 12, 0, 0, 0, DateTimeKind.Utc),
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = admin.Id,
            Action = "Seed",
            EntityType = "System",
            Detail = "Initial demo dataset created",
        });

        await db.SaveChangesAsync();
    }
}
