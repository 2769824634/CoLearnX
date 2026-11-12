using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// Older databases predate platform-owned courses. Staff tables and columns in such databases are left as they are.
public static class PlatformCourseSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var exists = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('Courses') WHERE name = 'IsPlatformOwned'").SingleAsync(ct);
            if (exists == 0)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Courses ADD COLUMN IsPlatformOwned INTEGER NOT NULL DEFAULT 0", ct);
        }
        else if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH('dbo.Courses', 'IsPlatformOwned') IS NULL
                    ALTER TABLE dbo.Courses ADD IsPlatformOwned bit NOT NULL CONSTRAINT DF_Courses_IsPlatformOwned DEFAULT 0;
                """, ct);
    }
}
