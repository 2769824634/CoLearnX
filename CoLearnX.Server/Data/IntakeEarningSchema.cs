using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class IntakeEarningSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS IntakeEarnings (
                    Id INTEGER NOT NULL CONSTRAINT PK_IntakeEarnings PRIMARY KEY AUTOINCREMENT,
                    CourseIntakeId INTEGER NOT NULL REFERENCES CourseIntakes(Id) ON DELETE RESTRICT,
                    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                    Role INTEGER NOT NULL,
                    EnrollmentId INTEGER NOT NULL REFERENCES Enrollments(Id) ON DELETE RESTRICT,
                    Credits INTEGER NOT NULL,
                    GrossCredits INTEGER NOT NULL,
                    IsReversal INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_IntakeEarnings_UserId_CreatedAt ON IntakeEarnings(UserId, CreatedAt);
                CREATE UNIQUE INDEX IF NOT EXISTS IX_IntakeEarnings_EnrollmentId_Role_Accrual
                    ON IntakeEarnings(EnrollmentId, Role) WHERE IsReversal = 0;
                """, ct);
        else if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.IntakeEarnings', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.IntakeEarnings (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_IntakeEarnings PRIMARY KEY,
                        CourseIntakeId int NOT NULL CONSTRAINT FK_IntakeEarnings_Intakes REFERENCES dbo.CourseIntakes(Id),
                        UserId int NOT NULL CONSTRAINT FK_IntakeEarnings_Users REFERENCES dbo.Users(Id),
                        Role int NOT NULL,
                        EnrollmentId int NOT NULL CONSTRAINT FK_IntakeEarnings_Enrollments REFERENCES dbo.Enrollments(Id),
                        Credits int NOT NULL,
                        GrossCredits int NOT NULL,
                        IsReversal bit NOT NULL,
                        CreatedAt datetime2 NOT NULL
                    );
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IntakeEarnings_UserId_CreatedAt' AND object_id = OBJECT_ID(N'dbo.IntakeEarnings'))
                    CREATE INDEX IX_IntakeEarnings_UserId_CreatedAt ON dbo.IntakeEarnings(UserId, CreatedAt);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IntakeEarnings_EnrollmentId_Role_Accrual' AND object_id = OBJECT_ID(N'dbo.IntakeEarnings'))
                    CREATE UNIQUE INDEX IX_IntakeEarnings_EnrollmentId_Role_Accrual ON dbo.IntakeEarnings(EnrollmentId, Role) WHERE IsReversal = 0;
                """, ct);
    }
}
