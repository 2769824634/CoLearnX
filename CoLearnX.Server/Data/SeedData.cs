using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// Demo users, courses, packages. Shared: SeedData
public static class SeedData
{
    public const string DemoPassword = "Password123!";
    public const string MemberEmail = "huang.yousheng@colearnx.com";
    public const string TrainerEmail = "gu.yincheng@colearnx.com";
    public const string CreatorEmail = "zou.ruiqi@colearnx.com";
    public const string AdminEmail = "zhu.zirui@colearnx.com";

    public static async Task InitializeAsync(CoLearnXDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        // EnsureCreated cannot upgrade an existing database. Fail before any seed writes.
        var hasB4CourseOwner = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM pragma_table_info('Courses') WHERE name = 'CreatorId'").SingleAsync() > 0;
        var hasB4Applications = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'CourseIntakeApplications'").SingleAsync() > 0;
        var hasLaterPhase = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'CertificateRequests'").SingleAsync() > 0;
        if (!hasB4CourseOwner || !hasB4Applications || !hasLaterPhase)
            throw new InvalidOperationException("Later Phase requires a fresh isolated database (colearnx-later-v1.db). The existing database was not migrated and was left unchanged.");
        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);

        var adminAccount = await db.AdminAccounts.SingleOrDefaultAsync(
            account => account.Email == AdminEmail);
        if (adminAccount is null)
        {
            adminAccount = new AdminAccount
            {
                Email = AdminEmail,
                PasswordHash = hash,
            };
            db.AdminAccounts.Add(adminAccount);
            await db.SaveChangesAsync();

            db.AuditLogs.Add(new AuditLog
            {
                AdminAccountId = adminAccount.Id,
                Action = "AdminAccountProvisioned",
                EntityType = nameof(AdminAccount),
                EntityId = adminAccount.Id.ToString(),
                Result = "Succeeded",
                Reason = "Initial demo administrator provisioned",
            });
            await db.SaveChangesAsync();
        }

        if (await db.Users.AnyAsync())
        {
            await EnsureRoleRequestFixturesAsync(db);
            await EnsureCourseReviewFixturesAsync(db);
            await EnsureLaterPhaseFixturesAsync(db);
            return;
        }

        var member = new User
        {
            Email = MemberEmail,
            PasswordHash = hash,
            FullName = "Huang Yousheng",
            DisplayName = "Yousheng",
            Phone = "12345678",
            Bio = "The sole disciple of the Way of Mercilessness",
            CreditBalance = 120,
        };
        var trainer = new User
        {
            Email = TrainerEmail,
            PasswordHash = hash,
            FullName = "Gu Yincheng",
            DisplayName = "Yincheng",
            Phone = "87654321",
            Bio = "Workshop facilitator · design thinking",
            CreditBalance = 0,
        };
        var creator = new User
        {
            Email = CreatorEmail,
            PasswordHash = hash,
            FullName = "Zou Ruiqi",
            DisplayName = "Ruiqi",
            Phone = "11223344",
            Bio = "Learning materials creator",
            CreditBalance = 40,
        };
        db.Users.AddRange(member, trainer, creator);
        await db.SaveChangesAsync();

        db.UserRoles.AddRange(
            new UserRole { UserId = member.Id, Role = AppRole.Member, IsVisible = true },
            new UserRole { UserId = trainer.Id, Role = AppRole.Trainer, IsVisible = true },
            new UserRole { UserId = creator.Id, Role = AppRole.Creator, IsVisible = true }
        );

        db.UserPreferences.AddRange(
            new UserPreference { UserId = member.Id, LearningGoals = "Career switch · UI/UX & Cybersecurity" },
            new UserPreference { UserId = trainer.Id },
            new UserPreference { UserId = creator.Id }
        );

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
            CreatorId = creator.Id,
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
            CreatorId = creator.Id,
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
            CreatorId = creator.Id,
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
            CreatorId = creator.Id,
            CreditCost = 35,
            Level = "Intermediate",
            Category = "Design",
            Status = CourseStatus.Published,
        };
        db.Courses.AddRange(c1, c2, c3, c4);
        await db.SaveChangesAsync();

        // Explicit demo fixtures, not evidence of Creator confirmation or the real B/C workflow.
        var demoIntakes = new[] { c1, c2, c3, c4 }.Select(course => new CourseIntake
        {
            CourseId = course.Id, TrainerId = trainer.Id,
            RegistrationOpensAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RegistrationClosesAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            StartsAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = CourseIntakeStatus.Published,
            ConfirmationNote = "Legacy Member demo fixture; not a Creator workflow result",
        }).ToArray();
        db.CourseIntakes.AddRange(demoIntakes);
        await db.SaveChangesAsync();

        var s1 = new CourseSession
        {
            CourseIntakeId = demoIntakes[0].Id,
            Label = "Session 1",
            StartsAt = new DateTime(2026, 5, 20, 9, 30, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 5, 20, 18, 30, 0, DateTimeKind.Utc),
            PhysicalCapacity = 20,
            SeatsTaken = 8,
        };
        var s2 = new CourseSession
        {
            CourseIntakeId = demoIntakes[0].Id,
            Label = "Session 2",
            StartsAt = new DateTime(2026, 6, 10, 9, 30, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 6, 10, 18, 30, 0, DateTimeKind.Utc),
            PhysicalCapacity = 20,
            SeatsTaken = 12,
        };
        var s3 = new CourseSession
        {
            CourseIntakeId = demoIntakes[1].Id,
            Label = "Session 1",
            StartsAt = new DateTime(2026, 5, 22, 14, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 5, 22, 18, 0, 0, DateTimeKind.Utc),
            PhysicalCapacity = 25,
            SeatsTaken = 10,
        };
        var s4 = new CourseSession
        {
            CourseIntakeId = demoIntakes[2].Id,
            Label = "Session 1",
            StartsAt = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 6, 15, 16, 0, 0, DateTimeKind.Utc),
            PhysicalCapacity = 30,
            SeatsTaken = 5,
        };
        var s5 = new CourseSession
        {
            CourseIntakeId = demoIntakes[3].Id,
            Label = "Session 1",
            StartsAt = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc),
            EndsAt = new DateTime(2026, 7, 1, 17, 0, 0, DateTimeKind.Utc),
            PhysicalCapacity = 18,
            SeatsTaken = 3,
        };
        db.CourseSessions.AddRange(s1, s2, s3, s4, s5);
        foreach (var session in new[] { s1, s2, s3, s4, s5 })
        {
            session.PhysicalAddress = "Demo training room";
            session.PhysicalBookingDeadline = session.StartsAt.AddDays(-1);
        }
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

        await db.SaveChangesAsync();
        await EnsureRoleRequestFixturesAsync(db);
        await EnsureCourseReviewFixturesAsync(db);
        await EnsureLaterPhaseFixturesAsync(db);
    }

    private static async Task EnsureRoleRequestFixturesAsync(CoLearnXDbContext db)
    {
        // Temporary D2 fixtures until Developer A supplies the user-side request flow.
        var member = await db.Users.SingleOrDefaultAsync(
            user => user.Email == MemberEmail);
        var creator = await db.Users.SingleOrDefaultAsync(
            user => user.Email == CreatorEmail);

        if (member is not null && !await db.RoleRequests.AnyAsync(
                request => request.UserId == member.Id && request.RequestedRole == AppRole.Creator))
        {
            db.RoleRequests.Add(new RoleRequest
            {
                UserId = member.Id,
                RequestedRole = AppRole.Creator,
                DegreeOrResumePath = "role-requests/huang-yousheng-portfolio.pdf",
                IdDocumentPath = "role-requests/huang-yousheng-id.pdf",
            });
        }

        if (creator is not null && !await db.RoleRequests.AnyAsync(
                request => request.UserId == creator.Id && request.RequestedRole == AppRole.Trainer))
        {
            db.RoleRequests.Add(new RoleRequest
            {
                UserId = creator.Id,
                RequestedRole = AppRole.Trainer,
                DegreeOrResumePath = "role-requests/zou-ruiqi-resume.pdf",
                IdDocumentPath = "role-requests/zou-ruiqi-id.pdf",
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureCourseReviewFixturesAsync(CoLearnXDbContext db)
    {
        // Temporary D3 fixture until Developer C supplies Course creation/submission.
        if (await db.Courses.AnyAsync(course => course.Code == "INFT 4025"))
            return;

        var creator = await db.Users.SingleOrDefaultAsync(
            user => user.Email == CreatorEmail);
        if (creator is null)
            return;

        db.Courses.Add(new Course
        {
            Code = "INFT 4025",
            Title = "Responsible AI for Learning Design",
            Description = "Design transparent, inclusive learning experiences with responsible AI practices.",
            TrainerId = creator.Id,
            CreatorId = creator.Id,
            CreditCost = 35,
            Level = "Advanced",
            Category = "Technology",
            Status = CourseStatus.PendingApproval,
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureLaterPhaseFixturesAsync(CoLearnXDbContext db)
    {
        var creator = await db.Users.SingleOrDefaultAsync(user => user.Email == CreatorEmail);
        var member = await db.Users.SingleOrDefaultAsync(user => user.Email == MemberEmail);
        var trainer = await db.Users.SingleOrDefaultAsync(user => user.Email == TrainerEmail);
        var admin = await db.AdminAccounts.SingleOrDefaultAsync(account => account.Email == AdminEmail);
        if (creator is null || member is null || trainer is null || admin is null) return;

        var approvedMaterial = await db.LearningMaterials.FirstOrDefaultAsync(material => material.Title == "UI/UX Basics");
        if (approvedMaterial is not null && !await db.CourseMaterialVersions.AnyAsync(version => version.LearningMaterialId == approvedMaterial.Id))
        {
            db.CourseMaterialVersions.Add(new CourseMaterialVersion
            {
                LearningMaterialId = approvedMaterial.Id,
                VersionNumber = approvedMaterial.Version,
                FilePath = approvedMaterial.FilePath,
                Format = approvedMaterial.Format,
                Status = MaterialVersionStatus.Approved,
                ReviewedByAdminAccountId = admin.Id,
                ReviewedAt = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            });
        }

        if (!await db.LearningMaterials.AnyAsync(material => material.Title == "Responsible AI Facilitation Pack"))
        {
            db.LearningMaterials.Add(new LearningMaterial
            {
                CreatorId = creator.Id,
                Title = "Responsible AI Facilitation Pack",
                Description = "Pending version for Admin Later Phase review.",
                FilePath = "materials/responsible-ai-facilitation-v1.pdf",
                Format = "PDF",
                Category = "Technology",
                Status = MaterialStatus.PendingReview,
                CourseMaterials = [],
            });
        }
        await db.SaveChangesAsync();

        var pendingMaterial = await db.LearningMaterials.SingleAsync(material => material.Title == "Responsible AI Facilitation Pack");
        if (!await db.CourseMaterialVersions.AnyAsync(version => version.LearningMaterialId == pendingMaterial.Id))
        {
            db.CourseMaterialVersions.Add(new CourseMaterialVersion
            {
                LearningMaterialId = pendingMaterial.Id,
                VersionNumber = 1,
                FilePath = pendingMaterial.FilePath,
                Format = pendingMaterial.Format,
                Status = MaterialVersionStatus.PendingApproval,
            });
        }

        var c1Enrollment = await db.Enrollments.Include(enrollment => enrollment.CourseSession)
            .FirstOrDefaultAsync(enrollment => enrollment.UserId == member.Id && enrollment.Course.Code == "INFT 2051");
        if (c1Enrollment is not null)
        {
            if (!await db.AttendanceRecords.AnyAsync(record => record.CourseSessionId == c1Enrollment.CourseSessionId && record.UserId == member.Id))
            {
                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    CourseSessionId = c1Enrollment.CourseSessionId,
                    UserId = member.Id,
                    Status = AttendanceStatus.Present,
                    RecordedByTrainerId = trainer.Id,
                });
            }
            if (!await db.Assessments.AnyAsync(assessment => assessment.CourseIntakeId == c1Enrollment.CourseSession.CourseIntakeId))
            {
                db.Assessments.Add(new Assessment
                {
                    CourseIntakeId = c1Enrollment.CourseSession.CourseIntakeId,
                    CreatedByTrainerId = trainer.Id,
                    Title = "Design critique",
                    MaxScore = 100,
                    PassScore = 60,
                    DueAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                });
            }
        }

        var c2Enrollment = await db.Enrollments.Include(enrollment => enrollment.Course)
            .FirstOrDefaultAsync(enrollment => enrollment.UserId == member.Id && enrollment.Course.Code == "INFT 3030");
        if (c2Enrollment is not null && !await db.Disputes.AnyAsync(dispute => dispute.EnrollmentId == c2Enrollment.Id))
        {
            db.Disputes.Add(new Dispute
            {
                RaisedByUserId = member.Id,
                EnrollmentId = c2Enrollment.Id,
                Reason = "I enrolled in the wrong session and need assistance.",
            });
        }
        await db.SaveChangesAsync();
    }
}
