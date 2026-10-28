using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class SessionRecordingFileSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var missing = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('SessionRecordings') WHERE name = 'FilePath'")
                .SingleAsync(ct) == 0;
            if (missing)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE SessionRecordings ADD COLUMN FilePath TEXT NULL", ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH('dbo.SessionRecordings', 'FilePath') IS NULL
                    ALTER TABLE dbo.SessionRecordings ADD FilePath nvarchar(512) NULL;
                """, ct);
        }
    }
}
