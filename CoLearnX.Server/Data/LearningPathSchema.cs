using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not upgrade an existing database. Preserve old path rows and
// classify only courses that already have a subject.
public static class LearningPathSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            if (await MissingColumnAsync(db, "LearningPaths", "CourseSubjectId", ct))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE LearningPaths ADD COLUMN CourseSubjectId INTEGER NULL REFERENCES CourseSubjects(Id)", ct);
            if (await MissingColumnAsync(db, "UserCertificates", "LearningPathId", ct))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserCertificates ADD COLUMN LearningPathId INTEGER NULL REFERENCES LearningPaths(Id)", ct);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS IX_LearningPaths_CourseSubjectId ON LearningPaths (CourseSubjectId) WHERE CourseSubjectId IS NOT NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS IX_UserCertificates_UserId_LearningPathId_CertificateTemplateId ON UserCertificates (UserId, LearningPathId, CertificateTemplateId) WHERE LearningPathId IS NOT NULL;
                CREATE TABLE IF NOT EXISTS LearningPathStages (
                    Id INTEGER NOT NULL CONSTRAINT PK_LearningPathStages PRIMARY KEY AUTOINCREMENT,
                    LearningPathId INTEGER NOT NULL REFERENCES LearningPaths(Id),
                    StageNumber INTEGER NOT NULL,
                    RequiredCourseCount INTEGER NOT NULL);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_LearningPathStages_LearningPathId_StageNumber ON LearningPathStages (LearningPathId, StageNumber);
                CREATE TABLE IF NOT EXISTS LearningPathCompletions (
                    Id INTEGER NOT NULL CONSTRAINT PK_LearningPathCompletions PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL REFERENCES Users(Id),
                    CourseId INTEGER NOT NULL REFERENCES Courses(Id),
                    LearningPathId INTEGER NOT NULL REFERENCES LearningPaths(Id),
                    StageNumber INTEGER NOT NULL,
                    CertificateId INTEGER NOT NULL REFERENCES UserCertificates(Id),
                    CompletedAt TEXT NOT NULL);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_LearningPathCompletions_UserId_CourseId ON LearningPathCompletions (UserId, CourseId);
                CREATE INDEX IF NOT EXISTS IX_LearningPathCompletions_UserId_LearningPathId_StageNumber ON LearningPathCompletions (UserId, LearningPathId, StageNumber);
                CREATE INDEX IF NOT EXISTS IX_LearningPathCompletions_CourseId ON LearningPathCompletions (CourseId);
                CREATE INDEX IF NOT EXISTS IX_LearningPathCompletions_LearningPathId ON LearningPathCompletions (LearningPathId);
                CREATE INDEX IF NOT EXISTS IX_LearningPathCompletions_CertificateId ON LearningPathCompletions (CertificateId);
                """, ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'dbo.LearningPaths', N'CourseSubjectId') IS NULL
                    ALTER TABLE dbo.LearningPaths ADD CourseSubjectId int NULL;
                IF COL_LENGTH(N'dbo.UserCertificates', N'LearningPathId') IS NULL
                    ALTER TABLE dbo.UserCertificates ADD LearningPathId int NULL;
                """, ct);
            await db.Database.ExecuteSqlRawAsync("""
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_LearningPaths_CourseSubjects_CourseSubjectId')
                    ALTER TABLE dbo.LearningPaths ADD CONSTRAINT FK_LearningPaths_CourseSubjects_CourseSubjectId FOREIGN KEY (CourseSubjectId) REFERENCES dbo.CourseSubjects(Id);
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_UserCertificates_LearningPaths_LearningPathId')
                    ALTER TABLE dbo.UserCertificates ADD CONSTRAINT FK_UserCertificates_LearningPaths_LearningPathId FOREIGN KEY (LearningPathId) REFERENCES dbo.LearningPaths(Id);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_LearningPaths_CourseSubjectId' AND object_id = OBJECT_ID(N'dbo.LearningPaths'))
                    CREATE UNIQUE INDEX IX_LearningPaths_CourseSubjectId ON dbo.LearningPaths (CourseSubjectId) WHERE CourseSubjectId IS NOT NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserCertificates_UserId_LearningPathId_CertificateTemplateId' AND object_id = OBJECT_ID(N'dbo.UserCertificates'))
                    CREATE UNIQUE INDEX IX_UserCertificates_UserId_LearningPathId_CertificateTemplateId ON dbo.UserCertificates (UserId, LearningPathId, CertificateTemplateId) WHERE LearningPathId IS NOT NULL;
                IF OBJECT_ID(N'dbo.LearningPathStages', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.LearningPathStages (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, LearningPathId int NOT NULL REFERENCES dbo.LearningPaths(Id), StageNumber int NOT NULL, RequiredCourseCount int NOT NULL);
                    CREATE UNIQUE INDEX IX_LearningPathStages_LearningPathId_StageNumber ON dbo.LearningPathStages (LearningPathId, StageNumber);
                END;
                IF OBJECT_ID(N'dbo.LearningPathCompletions', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.LearningPathCompletions (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, UserId int NOT NULL REFERENCES dbo.Users(Id), CourseId int NOT NULL REFERENCES dbo.Courses(Id), LearningPathId int NOT NULL REFERENCES dbo.LearningPaths(Id), StageNumber int NOT NULL, CertificateId int NOT NULL REFERENCES dbo.UserCertificates(Id), CompletedAt datetime2 NOT NULL);
                    CREATE UNIQUE INDEX IX_LearningPathCompletions_UserId_CourseId ON dbo.LearningPathCompletions (UserId, CourseId);
                    CREATE INDEX IX_LearningPathCompletions_UserId_LearningPathId_StageNumber ON dbo.LearningPathCompletions (UserId, LearningPathId, StageNumber);
                    CREATE INDEX IX_LearningPathCompletions_CourseId ON dbo.LearningPathCompletions (CourseId);
                    CREATE INDEX IX_LearningPathCompletions_LearningPathId ON dbo.LearningPathCompletions (LearningPathId);
                    CREATE INDEX IX_LearningPathCompletions_CertificateId ON dbo.LearningPathCompletions (CertificateId);
                END;
                """, ct);
        }
    }

    public static async Task SeedPathsAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        var paths = await db.LearningPaths.ToListAsync(ct);
        var subjects = await db.CourseSubjects.OrderBy(x => x.SortOrder).ToListAsync(ct);
        foreach (var subject in subjects)
        {
            var path = paths.SingleOrDefault(x => x.CourseSubjectId == subject.Id)
                ?? paths.FirstOrDefault(x => x.CourseSubjectId == null && x.Name.Equals(subject.Name, StringComparison.OrdinalIgnoreCase));
            if (path is null)
            {
                path = new LearningPath { Name = subject.Name, CourseSubjectId = subject.Id };
                db.LearningPaths.Add(path);
                paths.Add(path);
            }
            else path.CourseSubjectId = subject.Id;
        }
        await db.SaveChangesAsync(ct);

        var stages = await db.LearningPathStages.ToListAsync(ct);
        foreach (var path in paths.Where(x => x.CourseSubjectId != null))
            for (var number = 1; number <= 3; number++)
                if (!stages.Any(x => x.LearningPathId == path.Id && x.StageNumber == number))
                    db.LearningPathStages.Add(new LearningPathStage { LearningPathId = path.Id, StageNumber = number, RequiredCourseCount = 1 });

        var mapped = paths.Where(x => x.CourseSubjectId != null).ToDictionary(x => x.CourseSubjectId!.Value, x => x.Id);
        var levels = (await db.CourseLevels.AsNoTracking().ToListAsync(ct))
            .ToDictionary(x => x.Name.Trim(), x => x.Id, StringComparer.OrdinalIgnoreCase);
        var courses = await db.Courses.Where(x => x.CourseSubjectId != null).ToListAsync(ct);
        foreach (var course in courses)
        {
            if (mapped.TryGetValue(course.CourseSubjectId!.Value, out var pathId)) course.LearningPathId = pathId;
            if (course.CourseLevelId == null && !string.IsNullOrWhiteSpace(course.Level)
                && levels.TryGetValue(course.Level.Trim(), out var levelId)) course.CourseLevelId = levelId;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task<bool> MissingColumnAsync(CoLearnXDbContext db, string table, string column, CancellationToken ct)
        => await db.Database.SqlQuery<long>($"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}").SingleAsync(ct) == 0;
}
