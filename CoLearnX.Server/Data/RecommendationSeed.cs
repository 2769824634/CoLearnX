using System.Globalization;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class RecommendationSeed
{
    private const string NormalizedProfessionalGoal = "professional";
    private const string DemoRatingPrefix = "Demo seed rating:";
    private static readonly string[] HuangLegacyGoals = ["Career switch · UI/UX & Cybersecurity"];
    private static readonly string[] HuangDemoInterests = ["user-experience-design", "cybersecurity"];

    private static readonly DemoRatingSeed[] DemoRatings =
    [
        new("demo.reviewer.ux@colearnx.com", "Demo Reviewer UX", "UX Demo Reviewer", "INFT 2051", 5,
            "Clear design process", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
        new("demo.reviewer.frontend@colearnx.com", "Demo Reviewer Frontend", "Frontend Demo Reviewer", "INFT 2051", 4,
            "Useful practical exercises", new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc)),
        new("demo.reviewer.cyber@colearnx.com", "Demo Reviewer Cyber", "Cyber Demo Reviewer", "INFT 2051", 5,
            "Strong interaction examples", new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc)),
        new("demo.reviewer.ux@colearnx.com", "Demo Reviewer UX", "UX Demo Reviewer", "INFT 2002", 4,
            "Good React foundations", new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc)),
        new("demo.reviewer.frontend@colearnx.com", "Demo Reviewer Frontend", "Frontend Demo Reviewer", "INFT 2002", 5,
            "Well paced frontend practice", new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc)),
        new("demo.reviewer.cyber@colearnx.com", "Demo Reviewer Cyber", "Cyber Demo Reviewer", "INFT 2002", 4,
            "Helpful component walkthroughs", new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc)),
    ];

    private sealed record DemoRatingSeed(
        string Email,
        string FullName,
        string DisplayName,
        string CourseCode,
        int Stars,
        string Comment,
        DateTime CreatedAt);

    private static readonly (string Slug, string Name, string Leaves)[] Categories =
    [
        ("development", "Development", "web-development frontend-development backend-development mobile-development game-development programming-languages software-testing software-engineering devops no-code-development"),
        ("it-software", "IT & Software", "cybersecurity ethical-hacking network-security cloud-computing operating-systems it-certifications hardware it-operations"),
        ("data-ai", "Data Science & AI", "data-science machine-learning generative-ai data-engineering data-analysis business-intelligence"),
        ("design", "Design", "user-experience-design ui-visual-design ux-research web-design graphic-design design-systems 3d-animation game-design fashion-design interior-design"),
        ("art", "Art", "sketching drawing painting digital-painting illustration calligraphy comics"),
        ("business", "Business", "entrepreneurship management project-management product-management sales communication human-resources operations business-strategy e-commerce"),
        ("finance-accounting", "Finance & Accounting", "accounting finance investing financial-modeling cryptocurrency taxes"),
        ("marketing", "Marketing", "digital-marketing seo social-media-marketing content-marketing branding paid-advertising marketing-analytics"),
        ("office-productivity", "Office Productivity", "microsoft-office google-workspace apple-productivity sap data-entry-automation"),
        ("personal-development", "Personal Development", "career-development leadership personal-productivity public-speaking confidence stress-management"),
        ("photography-video", "Photography & Video", "digital-photography video-production photo-editing cinematography"),
        ("health-fitness", "Health & Fitness", "fitness yoga nutrition mental-health sports meditation"),
        ("music", "Music", "music-production instruments music-fundamentals vocals music-software"),
        ("language-learning", "Language Learning", "english academic-english chinese japanese korean spanish french german"),
        ("teaching-academics", "Teaching & Academics", "teacher-training math science humanities social-science test-prep engineering"),
    ];

    public static async Task EnsureSchemaAsync(CoLearnXDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            await SqliteColumn(db, "Interests", "Slug", "TEXT NULL");
            await SqliteColumn(db, "Interests", "ParentId", "INTEGER NULL");
            await SqliteColumn(db, "Interests", "SortOrder", "INTEGER NOT NULL DEFAULT 0");
            await SqliteColumn(db, "Interests", "IsActive", "INTEGER NOT NULL DEFAULT 1");
            await SqliteColumn(db, "UserPreferences", "OnboardingCompletedAt", "TEXT NULL");
            await SqliteColumn(db, "UserPreferences", "OnboardingSkippedAt", "TEXT NULL");
            await SqliteColumn(db, "ProgramRatings", "UpdatedAt", "TEXT NULL");
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS CourseInterests (CourseId INTEGER NOT NULL, InterestId INTEGER NOT NULL, PRIMARY KEY (CourseId, InterestId), FOREIGN KEY (CourseId) REFERENCES Courses(Id) ON DELETE CASCADE, FOREIGN KEY (InterestId) REFERENCES Interests(Id) ON DELETE RESTRICT)");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_Interests_Slug ON Interests(Slug)");
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_Interests_ParentId ON Interests(ParentId)");
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_CourseInterests_InterestId ON CourseInterests(InterestId)");
        }
        else if (db.Database.IsSqlServer())
        {
            await SqlServerColumn(db, "Interests", "Slug", "nvarchar(100) NULL");
            await SqlServerColumn(db, "Interests", "ParentId", "int NULL");
            await SqlServerColumn(db, "Interests", "SortOrder", "int NOT NULL DEFAULT 0");
            await SqlServerColumn(db, "Interests", "IsActive", "bit NOT NULL DEFAULT 1");
            await SqlServerColumn(db, "UserPreferences", "OnboardingCompletedAt", "datetime2 NULL");
            await SqlServerColumn(db, "UserPreferences", "OnboardingSkippedAt", "datetime2 NULL");
            await SqlServerColumn(db, "ProgramRatings", "UpdatedAt", "datetime2 NULL");
            await db.Database.ExecuteSqlRawAsync("IF OBJECT_ID(N'dbo.CourseInterests', N'U') IS NULL CREATE TABLE dbo.CourseInterests (CourseId int NOT NULL, InterestId int NOT NULL, CONSTRAINT PK_CourseInterests PRIMARY KEY (CourseId, InterestId), CONSTRAINT FK_CourseInterests_Courses_CourseId FOREIGN KEY (CourseId) REFERENCES dbo.Courses(Id) ON DELETE CASCADE, CONSTRAINT FK_CourseInterests_Interests_InterestId FOREIGN KEY (InterestId) REFERENCES dbo.Interests(Id))");
            await db.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Interests_Slug' AND object_id = OBJECT_ID(N'dbo.Interests')) CREATE UNIQUE INDEX IX_Interests_Slug ON dbo.Interests(Slug) WHERE Slug IS NOT NULL");
            await db.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Interests_ParentId' AND object_id = OBJECT_ID(N'dbo.Interests')) CREATE INDEX IX_Interests_ParentId ON dbo.Interests(ParentId)");
            await db.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseInterests_InterestId' AND object_id = OBJECT_ID(N'dbo.CourseInterests')) CREATE INDEX IX_CourseInterests_InterestId ON dbo.CourseInterests(InterestId)");
        }
    }

    private static async Task SqliteColumn(CoLearnXDbContext db, string table, string column, string type)
    {
        var count = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM pragma_table_info({0}) WHERE name = {1}", table, column).SingleAsync();
        if (count == 0)
        {
            // Identifiers and type come only from the fixed calls in EnsureSchemaAsync.
            var sql = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
    }

    private static async Task SqlServerColumn(CoLearnXDbContext db, string table, string column, string type)
    {
        // Identifiers and type come only from the fixed calls in EnsureSchemaAsync.
        var sql = $"IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL ALTER TABLE dbo.{table} ADD {column} {type}";
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    public static async Task EnsureInterestsAsync(CoLearnXDbContext db)
    {
        var existing = await db.Interests.Where(i => i.Slug != "").ToDictionaryAsync(i => i.Slug);
        for (var parentIndex = 0; parentIndex < Categories.Length; parentIndex++)
        {
            var category = Categories[parentIndex];
            if (!existing.TryGetValue(category.Slug, out var parent))
            {
                parent = new Interest { Slug = category.Slug, Name = category.Name };
                db.Interests.Add(parent);
                existing.Add(category.Slug, parent);
            }
            parent.Name = category.Name;
            parent.SortOrder = parentIndex + 1;
            parent.IsActive = true;
            await db.SaveChangesAsync();
            var leaves = category.Leaves.Split(' ');
            for (var leafIndex = 0; leafIndex < leaves.Length; leafIndex++)
            {
                var slug = leaves[leafIndex];
                if (!existing.TryGetValue(slug, out var leaf))
                {
                    leaf = new Interest { Slug = slug };
                    db.Interests.Add(leaf);
                    existing.Add(slug, leaf);
                }
                leaf.Name = LeafName(slug);
                leaf.ParentId = parent.Id;
                leaf.SortOrder = leafIndex + 1;
                leaf.IsActive = true;
            }
            await db.SaveChangesAsync();
        }
    }

    private static string LeafName(string slug) => slug switch
    {
        "user-experience-design" => "User Experience Design",
        "ui-visual-design" => "UI & Visual Design",
        "ux-research" => "UX Research",
        "graphic-design" => "Graphic Design & Illustration",
        "seo" => "SEO",
        "sap" => "SAP",
        "devops" => "DevOps",
        "3d-animation" => "3D Animation",
        _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug.Replace('-', ' ')),
    };

    public static async Task EnsureCourseTagsAsync(CoLearnXDbContext db)
    {
        var tags = new Dictionary<string, string[]>
        {
            ["INFT 2051"] = ["user-experience-design", "ui-visual-design"],
            ["INFT 2002"] = ["frontend-development", "web-development"],
            ["INFT 3030"] = ["cybersecurity", "network-security"],
            ["INFT 4010"] = ["ux-research", "user-experience-design"],
            ["INFT 4025"] = ["generative-ai", "user-experience-design"],
        };
        var courses = await db.Courses.Where(c => tags.Keys.Contains(c.Code)).Select(c => new { c.Id, c.Code }).ToListAsync();
        var interests = await db.Interests.Where(i => i.Slug != "").Select(i => new { i.Id, i.Slug }).ToDictionaryAsync(i => i.Slug, i => i.Id);
        var existing = await db.CourseInterests.Select(c => new { c.CourseId, c.InterestId }).ToListAsync();
        var pairs = existing.Select(c => (c.CourseId, c.InterestId)).ToHashSet();
        foreach (var course in courses.Where(c => !pairs.Any(pair => pair.CourseId == c.Id)))
            foreach (var slug in tags[course.Code])
                if (interests.TryGetValue(slug, out var interestId) && pairs.Add((course.Id, interestId)))
                    db.CourseInterests.Add(new CourseInterest { CourseId = course.Id, InterestId = interestId });
        await db.SaveChangesAsync();
    }

    public static async Task EnsureDemoProfileAndRatingsAsync(CoLearnXDbContext db)
    {
        var member = await db.Users.SingleOrDefaultAsync(user => user.Email == SeedData.MemberEmail);
        if (member is null)
            return;

        await EnsureHuangProfileAsync(db, member);

        var courseCodes = DemoRatings.Select(seed => seed.CourseCode).Distinct().ToArray();
        var courses = await db.Courses
            .Where(course => courseCodes.Contains(course.Code))
            .Include(course => course.Intakes)
            .ThenInclude(intake => intake.Sessions)
            .ToDictionaryAsync(course => course.Code);

        foreach (var seed in DemoRatings)
        {
            if (!courses.TryGetValue(seed.CourseCode, out var course))
                continue;

            var session = course.Intakes.OrderBy(intake => intake.Id)
                .SelectMany(intake => intake.Sessions.OrderBy(item => item.Id))
                .FirstOrDefault();
            if (session is null)
                continue;

            var reviewer = await EnsureDemoReviewerAsync(db, seed);
            var enrollment = await db.Enrollments
                .Where(item => item.UserId == reviewer.Id && item.CourseId == course.Id)
                .OrderBy(item => item.Id).FirstOrDefaultAsync();
            if (enrollment is null)
            {
                enrollment = new Enrollment
                {
                    UserId = reviewer.Id,
                    CourseId = course.Id,
                    CourseSessionId = session.Id,
                    Status = EnrollmentStatus.Completed,
                    ProgressPercent = 100,
                    CreditsSpent = 0,
                    EnrolledAt = seed.CreatedAt.AddDays(-14),
                    CompletedAt = seed.CreatedAt.AddDays(-7),
                };
                db.Enrollments.Add(enrollment);
                await db.SaveChangesAsync();
            }

            if (enrollment.Status != EnrollmentStatus.Completed)
                continue;

            var hasRating = await db.ProgramRatings.AnyAsync(rating => rating.EnrollmentId == enrollment.Id);
            if (!hasRating)
            {
                db.ProgramRatings.Add(new ProgramRating
                {
                    EnrollmentId = enrollment.Id,
                    Stars = seed.Stars,
                    Comment = $"{DemoRatingPrefix} {seed.Comment}",
                    CreatedAt = seed.CreatedAt,
                });
                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task EnsureHuangProfileAsync(CoLearnXDbContext db, User member)
    {
        var preference = await db.UserPreferences.SingleOrDefaultAsync(item => item.UserId == member.Id);
        var migrateLegacyDemoProfile = false;
        if (preference is null)
        {
            preference = new UserPreference { UserId = member.Id, LearningGoals = NormalizedProfessionalGoal };
            db.UserPreferences.Add(preference);
            migrateLegacyDemoProfile = true;
        }
        else if (HuangLegacyGoals.Contains(preference.LearningGoals?.Trim(), StringComparer.Ordinal))
        {
            preference.LearningGoals = NormalizedProfessionalGoal;
            migrateLegacyDemoProfile = true;
        }

        if (migrateLegacyDemoProfile && !await db.UserInterests.AnyAsync(item => item.UserId == member.Id))
        {
            var interests = await db.Interests
                .Where(item => HuangDemoInterests.Contains(item.Slug) && item.ParentId != null)
                .Select(item => item.Id)
                .ToListAsync();
            db.UserInterests.AddRange(interests.Select(interestId => new UserInterest
            {
                UserId = member.Id,
                InterestId = interestId,
            }));
        }

        if (migrateLegacyDemoProfile && preference.OnboardingCompletedAt is null && preference.OnboardingSkippedAt is null)
            preference.OnboardingCompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    private static async Task<User> EnsureDemoReviewerAsync(CoLearnXDbContext db, DemoRatingSeed seed)
    {
        var reviewer = await db.Users.SingleOrDefaultAsync(user => user.Email == seed.Email);
        if (reviewer is null)
        {
            reviewer = new User
            {
                Email = seed.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedData.DemoPassword),
                FullName = seed.FullName,
                DisplayName = seed.DisplayName,
                Bio = "Demo account used for seeded course ratings.",
                EmailVerifiedAt = seed.CreatedAt,
            };
            db.Users.Add(reviewer);
            await db.SaveChangesAsync();
        }

        if (!await db.UserRoles.AnyAsync(role => role.UserId == reviewer.Id && role.Role == AppRole.Member))
            db.UserRoles.Add(new UserRole { UserId = reviewer.Id, Role = AppRole.Member, IsVisible = true });
        if (!await db.UserPreferences.AnyAsync(preference => preference.UserId == reviewer.Id))
            db.UserPreferences.Add(new UserPreference { UserId = reviewer.Id });
        await db.SaveChangesAsync();
        return reviewer;
    }
}
