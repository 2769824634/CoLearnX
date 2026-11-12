using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class EarningsPayoutSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS PayoutRequests (
                    Id INTEGER NOT NULL CONSTRAINT PK_PayoutRequests PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                    Credits INTEGER NOT NULL,
                    PayoutMethod TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    Reference TEXT NULL,
                    ReviewedByAdminAccountId INTEGER NULL REFERENCES AdminAccounts(Id) ON DELETE RESTRICT,
                    RequestedAt TEXT NOT NULL,
                    ReviewedAt TEXT NULL
                );
                CREATE TABLE IF NOT EXISTS Earnings (
                    Id INTEGER NOT NULL CONSTRAINT PK_Earnings PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE RESTRICT,
                    CourseId INTEGER NOT NULL REFERENCES Courses(Id) ON DELETE RESTRICT,
                    CourseIntakeId INTEGER NOT NULL REFERENCES CourseIntakes(Id) ON DELETE RESTRICT,
                    EnrollmentId INTEGER NOT NULL REFERENCES Enrollments(Id) ON DELETE RESTRICT,
                    Role INTEGER NOT NULL,
                    Credits INTEGER NOT NULL,
                    SharePercent INTEGER NOT NULL,
                    Status INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    AvailableAt TEXT NOT NULL,
                    PayoutRequestId INTEGER NULL REFERENCES PayoutRequests(Id) ON DELETE RESTRICT
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_Earnings_EnrollmentId_Role ON Earnings(EnrollmentId, Role);
                CREATE INDEX IF NOT EXISTS IX_Earnings_UserId_Status ON Earnings(UserId, Status);
                CREATE INDEX IF NOT EXISTS IX_Earnings_UserId_Status_AvailableAt ON Earnings(UserId, Status, AvailableAt);
                CREATE INDEX IF NOT EXISTS IX_PayoutRequests_UserId_Status ON PayoutRequests(UserId, Status);
                """, ct);
        else if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.PayoutRequests', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.PayoutRequests (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PayoutRequests PRIMARY KEY,
                        UserId int NOT NULL CONSTRAINT FK_PayoutRequests_Users REFERENCES dbo.Users(Id),
                        Credits int NOT NULL, PayoutMethod nvarchar(256) NOT NULL,
                        Status int NOT NULL, Reference nvarchar(256) NULL,
                        ReviewedByAdminAccountId int NULL CONSTRAINT FK_PayoutRequests_AdminAccounts REFERENCES dbo.AdminAccounts(Id),
                        RequestedAt datetime2 NOT NULL, ReviewedAt datetime2 NULL
                    );
                END;
                IF OBJECT_ID(N'dbo.Earnings', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Earnings (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Earnings PRIMARY KEY,
                        UserId int NOT NULL CONSTRAINT FK_Earnings_Users REFERENCES dbo.Users(Id),
                        CourseId int NOT NULL CONSTRAINT FK_Earnings_Courses REFERENCES dbo.Courses(Id),
                        CourseIntakeId int NOT NULL CONSTRAINT FK_Earnings_CourseIntakes REFERENCES dbo.CourseIntakes(Id),
                        EnrollmentId int NOT NULL CONSTRAINT FK_Earnings_Enrollments REFERENCES dbo.Enrollments(Id),
                        Role int NOT NULL, Credits int NOT NULL, SharePercent int NOT NULL, Status int NOT NULL,
                        CreatedAt datetime2 NOT NULL, AvailableAt datetime2 NOT NULL,
                        PayoutRequestId int NULL CONSTRAINT FK_Earnings_PayoutRequests REFERENCES dbo.PayoutRequests(Id)
                    );
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Earnings_EnrollmentId_Role' AND object_id=OBJECT_ID(N'dbo.Earnings'))
                    CREATE UNIQUE INDEX IX_Earnings_EnrollmentId_Role ON dbo.Earnings(EnrollmentId, Role);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Earnings_UserId_Status_AvailableAt' AND object_id=OBJECT_ID(N'dbo.Earnings'))
                    CREATE INDEX IX_Earnings_UserId_Status_AvailableAt ON dbo.Earnings(UserId, Status, AvailableAt);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_Earnings_UserId_Status' AND object_id=OBJECT_ID(N'dbo.Earnings'))
                    CREATE INDEX IX_Earnings_UserId_Status ON dbo.Earnings(UserId, Status);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_PayoutRequests_UserId_Status' AND object_id=OBJECT_ID(N'dbo.PayoutRequests'))
                    CREATE INDEX IX_PayoutRequests_UserId_Status ON dbo.PayoutRequests(UserId, Status);
                """, ct);
    }
}

// Public design name retained for callers and tests; the implementation covers both related tables.
public static class EarningsSchema
{
    public static Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
        => EarningsPayoutSchema.EnsureAsync(db, ct);
}
