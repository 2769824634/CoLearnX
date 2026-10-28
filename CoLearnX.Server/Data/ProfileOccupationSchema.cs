using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not add properties to existing databases.
public static class ProfileOccupationSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var exists = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('Users') WHERE name = 'Occupation'").SingleAsync(ct);
            if (exists == 0)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN Occupation TEXT NOT NULL DEFAULT 'unknown'", ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'dbo.Users', N'Occupation') IS NULL
                    ALTER TABLE dbo.Users ADD Occupation nvarchar(120) NOT NULL
                        CONSTRAINT DF_Users_Occupation DEFAULT (N'unknown') WITH VALUES;
                """, ct);
        }
    }
}
