using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class MaterialUsageSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.MaterialUsageLogs', N'U') IS NOT NULL
                   AND COL_LENGTH(N'dbo.MaterialUsageLogs', N'CourseIntakeId') IS NULL
                    ALTER TABLE dbo.MaterialUsageLogs ADD CourseIntakeId int NULL;
                """, ct);
        else if (db.Database.IsSqlite())
        {
            var exists = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('MaterialUsageLogs') WHERE name = 'CourseIntakeId'")
                .SingleAsync(ct);
            if (exists == 0)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE MaterialUsageLogs ADD COLUMN CourseIntakeId INTEGER NULL", ct);
        }
    }
}
