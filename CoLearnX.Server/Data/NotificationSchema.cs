using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class NotificationSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db)
    {
        foreach (var (name, sqlite, sqlServer) in new[] {
            ("IntakeId", "INTEGER NULL", "int NULL"),
            ("EnrollmentId", "INTEGER NULL", "int NULL"),
            ("EmailPending", "INTEGER NOT NULL DEFAULT 0", "bit NOT NULL DEFAULT 0"),
            ("EmailAttemptedAt", "TEXT NULL", "datetime2 NULL"),
            ("EmailSentAt", "TEXT NULL", "datetime2 NULL") })
        {
            // Fixed identifiers; legacy notifications stay in-app only, with a list-page fallback.
            if (db.Database.IsSqlite())
            {
                if (await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Notifications') WHERE name = {0}", name).SingleAsync() == 0)
                {
                    var sql = $"ALTER TABLE Notifications ADD COLUMN {name} {sqlite}";
                    await db.Database.ExecuteSqlRawAsync(sql);
                }
            }
            else if (db.Database.IsSqlServer())
            {
                var sql = $"IF COL_LENGTH(N'dbo.Notifications', N'{name}') IS NULL ALTER TABLE dbo.Notifications ADD {name} {sqlServer}";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }
    }
}
