using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not add properties to existing databases.
public static class ProfileCountrySchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var exists = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('Users') WHERE name = 'Country'").SingleAsync(ct);
            if (exists == 0)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN Country TEXT NULL", ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'dbo.Users', N'Country') IS NULL
                    ALTER TABLE dbo.Users ADD Country nvarchar(120) NULL;
                """, ct);
        }
    }
}
