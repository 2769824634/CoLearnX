using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not add tables or columns to a database that already exists.
// Roles that existed before certification was introduced are backfilled once, when the column is added.
public static class RoleCertificationSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            if (await MissingColumnAsync(db, "UserRoles", "CertifiedAt", ct))
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserRoles ADD COLUMN CertifiedAt TEXT NULL", ct);
                await db.Database.ExecuteSqlRawAsync("UPDATE UserRoles SET CertifiedAt = GrantedAt", ct);
            }
            if (await MissingColumnAsync(db, "RoleRequests", "SpecialtyIds", ct))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE RoleRequests ADD COLUMN SpecialtyIds TEXT NULL", ct);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS UserSpecialties (
                    UserId INTEGER NOT NULL,
                    CourseSubjectId INTEGER NOT NULL,
                    CONSTRAINT PK_UserSpecialties PRIMARY KEY (UserId, CourseSubjectId),
                    CONSTRAINT FK_UserSpecialties_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE,
                    CONSTRAINT FK_UserSpecialties_CourseSubjects_CourseSubjectId FOREIGN KEY (CourseSubjectId) REFERENCES CourseSubjects (Id) ON DELETE RESTRICT);
                CREATE INDEX IF NOT EXISTS IX_UserSpecialties_CourseSubjectId ON UserSpecialties (CourseSubjectId);
                """, ct);
        }
        else if (db.Database.IsSqlServer())
        {
            // The backfill mentions the new column, so it must run in a later batch.
            var added = await db.Database.SqlQuery<int>($"""
                SELECT CASE WHEN COL_LENGTH(N'dbo.UserRoles', N'CertifiedAt') IS NULL THEN 1 ELSE 0 END AS Value
                """).SingleAsync(ct) == 1;
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'dbo.UserRoles', N'CertifiedAt') IS NULL
                    ALTER TABLE dbo.UserRoles ADD CertifiedAt datetime2 NULL;
                IF COL_LENGTH(N'dbo.RoleRequests', N'SpecialtyIds') IS NULL
                    ALTER TABLE dbo.RoleRequests ADD SpecialtyIds nvarchar(max) NULL;
                IF OBJECT_ID(N'dbo.UserSpecialties', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.UserSpecialties (
                        UserId int NOT NULL,
                        CourseSubjectId int NOT NULL,
                        CONSTRAINT PK_UserSpecialties PRIMARY KEY (UserId, CourseSubjectId),
                        CONSTRAINT FK_UserSpecialties_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
                        CONSTRAINT FK_UserSpecialties_CourseSubjects_CourseSubjectId FOREIGN KEY (CourseSubjectId) REFERENCES dbo.CourseSubjects (Id));
                    CREATE INDEX IX_UserSpecialties_CourseSubjectId ON dbo.UserSpecialties (CourseSubjectId);
                END;
                """, ct);
            if (added)
                await db.Database.ExecuteSqlRawAsync("UPDATE dbo.UserRoles SET CertifiedAt = GrantedAt", ct);
        }
    }

    public const string SingleRoleIndexName = "UX_UserRoles_UserId";

    // One account holds one role. Run after multi-role accounts are cleaned up: while any remain,
    // the index is not created and their user ids are returned so startup can warn instead of failing.
    public static async Task<IReadOnlyList<int>> EnsureSingleRoleIndexAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        var duplicates = await db.Database.SqlQuery<int>(
            $"SELECT UserId AS Value FROM UserRoles GROUP BY UserId HAVING COUNT(*) > 1")
            .ToListAsync(ct);
        if (duplicates.Count > 0)
            return duplicates;

        if (db.Database.IsSqlite())
            await db.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS UX_UserRoles_UserId ON UserRoles (UserId);", ct);
        else if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_UserRoles_UserId' AND object_id = OBJECT_ID(N'dbo.UserRoles'))
                    CREATE UNIQUE INDEX UX_UserRoles_UserId ON dbo.UserRoles (UserId);
                """, ct);
        return [];
    }

    private static async Task<bool> MissingColumnAsync(CoLearnXDbContext db, string table, string column, CancellationToken ct)
    {
        var count = await db.Database.SqlQuery<long>(
            $"SELECT COUNT(*) AS Value FROM pragma_table_info({table}) WHERE name = {column}")
            .SingleAsync(ct);
        return count == 0;
    }
}
