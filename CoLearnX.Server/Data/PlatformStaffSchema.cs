using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class PlatformStaffSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
            await EnsureSqliteAsync(db, ct);
        else if (db.Database.IsSqlServer())
            await EnsureSqlServerAsync(db, ct);
    }

    private static async Task EnsureSqliteAsync(CoLearnXDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS StaffAccounts (
                Id INTEGER NOT NULL CONSTRAINT PK_StaffAccounts PRIMARY KEY AUTOINCREMENT,
                Email TEXT COLLATE NOCASE NOT NULL,
                PasswordHash TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                SessionStamp TEXT NOT NULL,
                FullName TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                Phone TEXT NULL,
                StaffNumber INTEGER NOT NULL,
                DocumentPath TEXT NOT NULL,
                UserId INTEGER NOT NULL,
                CreatedByAdminAccountId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CONSTRAINT FK_StaffAccounts_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT,
                CONSTRAINT FK_StaffAccounts_AdminAccounts_CreatedByAdminAccountId FOREIGN KEY (CreatedByAdminAccountId) REFERENCES AdminAccounts (Id) ON DELETE RESTRICT);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_StaffAccounts_Email ON StaffAccounts (Email);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_StaffAccounts_StaffNumber ON StaffAccounts (StaffNumber);
            CREATE UNIQUE INDEX IF NOT EXISTS IX_StaffAccounts_UserId ON StaffAccounts (UserId);
            CREATE INDEX IF NOT EXISTS IX_StaffAccounts_CreatedByAdminAccountId ON StaffAccounts (CreatedByAdminAccountId);
            """, ct);

        if (await ColumnMissingAsync(db, "Courses", "IsPlatformOwned", ct))
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Courses ADD COLUMN IsPlatformOwned INTEGER NOT NULL DEFAULT 0", ct);

        if (!await ColumnMissingAsync(db, "AuditLogs", "StaffAccountId", ct))
            return;

        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF", ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE AuditLogs RENAME TO AuditLogs_PlatformStaffLegacy;
                CREATE TABLE AuditLogs (
                    Id INTEGER NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY AUTOINCREMENT,
                    AdminAccountId INTEGER NULL,
                    StaffAccountId INTEGER NULL,
                    UserId INTEGER NULL,
                    Action TEXT NOT NULL,
                    EntityType TEXT NOT NULL,
                    EntityId TEXT NULL,
                    Result TEXT NOT NULL,
                    Reason TEXT NULL,
                    CreatedAt TEXT NOT NULL,
                    CONSTRAINT CK_AuditLogs_ExactlyOneActor CHECK (
                        (CASE WHEN AdminAccountId IS NULL THEN 0 ELSE 1 END
                        + CASE WHEN StaffAccountId IS NULL THEN 0 ELSE 1 END
                        + CASE WHEN UserId IS NULL THEN 0 ELSE 1 END) = 1),
                    CONSTRAINT FK_AuditLogs_AdminAccounts_AdminAccountId FOREIGN KEY (AdminAccountId) REFERENCES AdminAccounts (Id) ON DELETE RESTRICT,
                    CONSTRAINT FK_AuditLogs_StaffAccounts_StaffAccountId FOREIGN KEY (StaffAccountId) REFERENCES StaffAccounts (Id) ON DELETE RESTRICT,
                    CONSTRAINT FK_AuditLogs_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE RESTRICT);
                INSERT INTO AuditLogs (Id, AdminAccountId, StaffAccountId, UserId, Action, EntityType, EntityId, Result, Reason, CreatedAt)
                    SELECT Id, AdminAccountId, NULL, UserId, Action, EntityType, EntityId, Result, Reason, CreatedAt
                    FROM AuditLogs_PlatformStaffLegacy;
                DROP TABLE AuditLogs_PlatformStaffLegacy;
                CREATE INDEX IX_AuditLogs_AdminAccountId ON AuditLogs (AdminAccountId);
                CREATE INDEX IX_AuditLogs_StaffAccountId ON AuditLogs (StaffAccountId);
                CREATE INDEX IX_AuditLogs_UserId ON AuditLogs (UserId);
                CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs (CreatedAt);
                """, ct);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON", ct);
        }
    }

    private static async Task EnsureSqlServerAsync(CoLearnXDbContext db, CancellationToken ct)
    {
        // SQL Server compiles a batch before running it. Statements that mention StaffAccountId
        // must run after the column has been added, in a later batch.
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.StaffAccounts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.StaffAccounts (
                    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StaffAccounts PRIMARY KEY,
                    Email nvarchar(256) NOT NULL,
                    PasswordHash nvarchar(128) NOT NULL,
                    IsActive bit NOT NULL CONSTRAINT DF_StaffAccounts_IsActive DEFAULT 1,
                    SessionStamp uniqueidentifier NOT NULL,
                    FullName nvarchar(128) NOT NULL,
                    DisplayName nvarchar(64) NOT NULL,
                    Phone nvarchar(32) NULL,
                    StaffNumber int NOT NULL,
                    DocumentPath nvarchar(512) NOT NULL,
                    UserId int NOT NULL,
                    CreatedByAdminAccountId int NOT NULL,
                    CreatedAt datetime2 NOT NULL,
                    UpdatedAt datetime2 NOT NULL,
                    CONSTRAINT FK_StaffAccounts_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id),
                    CONSTRAINT FK_StaffAccounts_AdminAccounts_CreatedByAdminAccountId FOREIGN KEY (CreatedByAdminAccountId) REFERENCES dbo.AdminAccounts(Id));
                CREATE UNIQUE INDEX IX_StaffAccounts_Email ON dbo.StaffAccounts(Email);
                CREATE UNIQUE INDEX IX_StaffAccounts_StaffNumber ON dbo.StaffAccounts(StaffNumber);
                CREATE UNIQUE INDEX IX_StaffAccounts_UserId ON dbo.StaffAccounts(UserId);
                CREATE INDEX IX_StaffAccounts_CreatedByAdminAccountId ON dbo.StaffAccounts(CreatedByAdminAccountId);
            END;
            IF COL_LENGTH('dbo.Courses', 'IsPlatformOwned') IS NULL
                ALTER TABLE dbo.Courses ADD IsPlatformOwned bit NOT NULL CONSTRAINT DF_Courses_IsPlatformOwned DEFAULT 0;
            IF COL_LENGTH('dbo.AuditLogs', 'StaffAccountId') IS NULL
                ALTER TABLE dbo.AuditLogs ADD StaffAccountId int NULL;
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AuditLogs_ExactlyOneActor')
                ALTER TABLE dbo.AuditLogs DROP CONSTRAINT CK_AuditLogs_ExactlyOneActor;
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AuditLogs_StaffAccounts_StaffAccountId')
                ALTER TABLE dbo.AuditLogs ADD CONSTRAINT FK_AuditLogs_StaffAccounts_StaffAccountId
                    FOREIGN KEY (StaffAccountId) REFERENCES dbo.StaffAccounts(Id);
            ALTER TABLE dbo.AuditLogs ADD CONSTRAINT CK_AuditLogs_ExactlyOneActor CHECK (
                (CASE WHEN AdminAccountId IS NULL THEN 0 ELSE 1 END
                + CASE WHEN StaffAccountId IS NULL THEN 0 ELSE 1 END
                + CASE WHEN UserId IS NULL THEN 0 ELSE 1 END) = 1);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_StaffAccountId' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
                CREATE INDEX IX_AuditLogs_StaffAccountId ON dbo.AuditLogs(StaffAccountId);
            """, ct);
    }

    private static async Task<bool> ColumnMissingAsync(
        CoLearnXDbContext db,
        string table,
        string column,
        CancellationToken ct)
    {
        var count = await db.Database.SqlQuery<long>(
            $"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}")
            .SingleAsync(ct);
        return count == 0;
    }
}
