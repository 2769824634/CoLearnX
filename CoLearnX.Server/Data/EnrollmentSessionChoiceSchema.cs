using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class EnrollmentSessionChoiceSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS EnrollmentSessionChoices (
                    EnrollmentId INTEGER NOT NULL REFERENCES Enrollments(Id) ON DELETE RESTRICT,
                    CourseSessionId INTEGER NOT NULL REFERENCES CourseSessions(Id) ON DELETE RESTRICT,
                    Mode TEXT NOT NULL,
                    CONSTRAINT PK_EnrollmentSessionChoices PRIMARY KEY (EnrollmentId, CourseSessionId)
                );
                CREATE INDEX IF NOT EXISTS IX_EnrollmentSessionChoices_CourseSessionId ON EnrollmentSessionChoices(CourseSessionId);
                """, ct);
        else if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.EnrollmentSessionChoices', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.EnrollmentSessionChoices (
                        EnrollmentId int NOT NULL CONSTRAINT FK_EnrollmentSessionChoices_Enrollments REFERENCES dbo.Enrollments(Id),
                        CourseSessionId int NOT NULL CONSTRAINT FK_EnrollmentSessionChoices_Sessions REFERENCES dbo.CourseSessions(Id),
                        Mode nvarchar(16) NOT NULL,
                        CONSTRAINT PK_EnrollmentSessionChoices PRIMARY KEY (EnrollmentId, CourseSessionId)
                    );
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EnrollmentSessionChoices_CourseSessionId' AND object_id = OBJECT_ID(N'dbo.EnrollmentSessionChoices'))
                    CREATE INDEX IX_EnrollmentSessionChoices_CourseSessionId ON dbo.EnrollmentSessionChoices(CourseSessionId);
                """, ct);
    }
}
