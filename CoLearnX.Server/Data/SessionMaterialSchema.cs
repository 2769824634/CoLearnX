using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not alter a database that already contains the v8 tables.
// Keep this small idempotent upgrade alongside the existing schema probes so a
// v8 SQLite or SQL Server database receives the new session-material table.
public static class SessionMaterialSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var exists = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'SessionMaterials'")
                .SingleAsync(ct);
            if (exists == 0)
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE SessionMaterials (
                        Id INTEGER NOT NULL CONSTRAINT PK_SessionMaterials PRIMARY KEY AUTOINCREMENT,
                        CourseSessionId INTEGER NOT NULL,
                        AddedByTrainerId INTEGER NOT NULL,
                        Title TEXT NOT NULL,
                        FilePath TEXT NOT NULL,
                        Format TEXT NOT NULL,
                        UploadedAt TEXT NOT NULL,
                        CONSTRAINT FK_SessionMaterials_CourseSessions_CourseSessionId
                            FOREIGN KEY (CourseSessionId) REFERENCES CourseSessions (Id) ON DELETE RESTRICT
                    )
                    """, ct);
            }

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS IX_SessionMaterials_CourseSessionId_UploadedAt
                    ON SessionMaterials (CourseSessionId, UploadedAt)
                """, ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.SessionMaterials', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.SessionMaterials (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SessionMaterials PRIMARY KEY,
                        CourseSessionId int NOT NULL,
                        AddedByTrainerId int NOT NULL,
                        Title nvarchar(160) NOT NULL,
                        FilePath nvarchar(512) NOT NULL,
                        Format nvarchar(32) NOT NULL,
                        UploadedAt datetime2 NOT NULL,
                        CONSTRAINT FK_SessionMaterials_CourseSessions_CourseSessionId
                            FOREIGN KEY (CourseSessionId) REFERENCES dbo.CourseSessions (Id) ON DELETE NO ACTION
                    );
                END;
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_SessionMaterials_CourseSessionId_UploadedAt'
                      AND object_id = OBJECT_ID(N'dbo.SessionMaterials'))
                    CREATE INDEX IX_SessionMaterials_CourseSessionId_UploadedAt
                        ON dbo.SessionMaterials (CourseSessionId, UploadedAt);
                """, ct);
        }
    }
}
