using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not alter a database that already exists, so an existing SQLite or
// SQL Server database receives the Member assignment-file table here. Safe to run repeatedly.
public static class AssessmentSubmissionSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS AssessmentSubmissionFiles (
                    Id INTEGER NOT NULL CONSTRAINT PK_AssessmentSubmissionFiles PRIMARY KEY AUTOINCREMENT,
                    AssessmentId INTEGER NOT NULL,
                    EnrollmentId INTEGER NOT NULL,
                    OriginalFileName TEXT NOT NULL,
                    FilePath TEXT NOT NULL,
                    Format TEXT NOT NULL,
                    SizeBytes INTEGER NOT NULL,
                    SubmittedAt TEXT NOT NULL,
                    CONSTRAINT FK_AssessmentSubmissionFiles_Assessments_AssessmentId
                        FOREIGN KEY (AssessmentId) REFERENCES Assessments (Id) ON DELETE RESTRICT,
                    CONSTRAINT FK_AssessmentSubmissionFiles_Enrollments_EnrollmentId
                        FOREIGN KEY (EnrollmentId) REFERENCES Enrollments (Id) ON DELETE RESTRICT
                )
                """, ct);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS IX_AssessmentSubmissionFiles_AssessmentId_EnrollmentId
                    ON AssessmentSubmissionFiles (AssessmentId, EnrollmentId)
                """, ct);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS IX_AssessmentSubmissionFiles_EnrollmentId
                    ON AssessmentSubmissionFiles (EnrollmentId)
                """, ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.AssessmentSubmissionFiles', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.AssessmentSubmissionFiles (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_AssessmentSubmissionFiles PRIMARY KEY,
                        AssessmentId int NOT NULL,
                        EnrollmentId int NOT NULL,
                        OriginalFileName nvarchar(260) NOT NULL,
                        FilePath nvarchar(512) NOT NULL,
                        Format nvarchar(32) NOT NULL,
                        SizeBytes bigint NOT NULL,
                        SubmittedAt datetime2 NOT NULL,
                        CONSTRAINT FK_AssessmentSubmissionFiles_Assessments_AssessmentId
                            FOREIGN KEY (AssessmentId) REFERENCES dbo.Assessments (Id) ON DELETE NO ACTION,
                        CONSTRAINT FK_AssessmentSubmissionFiles_Enrollments_EnrollmentId
                            FOREIGN KEY (EnrollmentId) REFERENCES dbo.Enrollments (Id) ON DELETE NO ACTION
                    );
                END;
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_AssessmentSubmissionFiles_AssessmentId_EnrollmentId'
                      AND object_id = OBJECT_ID(N'dbo.AssessmentSubmissionFiles'))
                    CREATE INDEX IX_AssessmentSubmissionFiles_AssessmentId_EnrollmentId
                        ON dbo.AssessmentSubmissionFiles (AssessmentId, EnrollmentId);
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_AssessmentSubmissionFiles_EnrollmentId'
                      AND object_id = OBJECT_ID(N'dbo.AssessmentSubmissionFiles'))
                    CREATE INDEX IX_AssessmentSubmissionFiles_EnrollmentId
                        ON dbo.AssessmentSubmissionFiles (EnrollmentId);
                """, ct);
        }
    }
}
