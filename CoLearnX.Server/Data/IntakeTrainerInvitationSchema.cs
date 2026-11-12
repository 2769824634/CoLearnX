using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not add columns to a database that already exists.
public static class IntakeTrainerInvitationSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            await AddSqliteColumnAsync(db, "TrainerInvitation", "INTEGER NOT NULL DEFAULT 0", ct);
            await AddSqliteColumnAsync(db, "TrainerInvitedAt", "TEXT NULL", ct);
            await AddSqliteColumnAsync(db, "TrainerRespondedAt", "TEXT NULL", ct);
            await AddSqliteColumnAsync(db, "TrainerDeclineReason", "TEXT NULL", ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'dbo.CourseIntakes', N'TrainerInvitation') IS NULL
                    ALTER TABLE dbo.CourseIntakes ADD TrainerInvitation int NOT NULL CONSTRAINT DF_CourseIntakes_TrainerInvitation DEFAULT 0;
                IF COL_LENGTH(N'dbo.CourseIntakes', N'TrainerInvitedAt') IS NULL
                    ALTER TABLE dbo.CourseIntakes ADD TrainerInvitedAt datetime2 NULL;
                IF COL_LENGTH(N'dbo.CourseIntakes', N'TrainerRespondedAt') IS NULL
                    ALTER TABLE dbo.CourseIntakes ADD TrainerRespondedAt datetime2 NULL;
                IF COL_LENGTH(N'dbo.CourseIntakes', N'TrainerDeclineReason') IS NULL
                    ALTER TABLE dbo.CourseIntakes ADD TrainerDeclineReason nvarchar(500) NULL;
                """, ct);
        }

        // Intakes published before this step existed were assigned directly; their Trainer is already teaching them.
        // Status 0 is Draft and 5 is Rejected; every other status has been published at least once.
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE CourseIntakes SET TrainerInvitation = 2, TrainerRespondedAt = ConfirmedAt
            WHERE TrainerInvitation = 0 AND Status NOT IN (0, 5)
            """, ct);
    }

    private static async Task AddSqliteColumnAsync(CoLearnXDbContext db, string column, string definition, CancellationToken ct)
    {
        var exists = await db.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS Value FROM pragma_table_info('CourseIntakes') WHERE name = {column}").SingleAsync(ct);
        if (exists == 0)
            await db.Database.ExecuteSqlRawAsync($"ALTER TABLE CourseIntakes ADD COLUMN {column} {definition}", ct);
    }
}
