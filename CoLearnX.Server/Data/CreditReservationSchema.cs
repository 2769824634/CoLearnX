using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class CreditReservationSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db)
    {
        if (db.Database.IsSqlite())
        {
            await AddSqliteColumn(db, "Users", "HeldCredits", "INTEGER NOT NULL DEFAULT 0");
            await AddSqliteColumn(db, "CreditTransactions", "HeldAfter", "INTEGER NULL");
            await AddSqliteColumn(db, "CourseIntakes", "MinEnrollment", "INTEGER NOT NULL DEFAULT 10");
            await AddSqliteColumn(db, "CourseIntakes", "ConfirmedToRunAt", "TEXT NULL");
            await AddSqliteColumn(db, "CourseIntakes", "CancelledAt", "TEXT NULL");
            await AddSqliteColumn(db, "CourseIntakes", "CancellationReason", "TEXT NULL");
            await AddSqliteColumn(db, "CourseIntakes", "ReplacementForIntakeId", "INTEGER NULL REFERENCES CourseIntakes(Id)");
            await AddSqliteColumn(db, "Enrollments", "PostponementEligible", "INTEGER NOT NULL DEFAULT 0");
            await AddSqliteColumn(db, "Enrollments", "PostponedFromEnrollmentId", "INTEGER NULL REFERENCES Enrollments(Id)");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_CourseIntakes_ReplacementForIntakeId ON CourseIntakes(ReplacementForIntakeId) WHERE ReplacementForIntakeId IS NOT NULL");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_Enrollments_PostponedFromEnrollmentId ON Enrollments(PostponedFromEnrollmentId) WHERE PostponedFromEnrollmentId IS NOT NULL");
            await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX IF NOT EXISTS IX_CreditTransactions_RelatedEnrollmentId_Type ON CreditTransactions(RelatedEnrollmentId, Type) WHERE RelatedEnrollmentId IS NOT NULL AND Type IN (6, 7, 8)");
        }
        else if (db.Database.IsSqlServer())
        {
            await AddSqlServerColumn(db, "Users", "HeldCredits", "int NOT NULL DEFAULT 0");
            await AddSqlServerColumn(db, "CreditTransactions", "HeldAfter", "int NULL");
            await AddSqlServerColumn(db, "CourseIntakes", "MinEnrollment", "int NOT NULL DEFAULT 10");
            await AddSqlServerColumn(db, "CourseIntakes", "ConfirmedToRunAt", "datetime2 NULL");
            await AddSqlServerColumn(db, "CourseIntakes", "CancelledAt", "datetime2 NULL");
            await AddSqlServerColumn(db, "CourseIntakes", "CancellationReason", "nvarchar(64) NULL");
            await AddSqlServerColumn(db, "CourseIntakes", "ReplacementForIntakeId", "int NULL REFERENCES dbo.CourseIntakes(Id)");
            await AddSqlServerColumn(db, "Enrollments", "PostponementEligible", "bit NOT NULL DEFAULT 0");
            await AddSqlServerColumn(db, "Enrollments", "PostponedFromEnrollmentId", "int NULL REFERENCES dbo.Enrollments(Id)");
            await db.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseIntakes_ReplacementForIntakeId' AND object_id = OBJECT_ID(N'dbo.CourseIntakes')) CREATE UNIQUE INDEX IX_CourseIntakes_ReplacementForIntakeId ON dbo.CourseIntakes(ReplacementForIntakeId) WHERE ReplacementForIntakeId IS NOT NULL");
            await db.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Enrollments_PostponedFromEnrollmentId' AND object_id = OBJECT_ID(N'dbo.Enrollments')) CREATE UNIQUE INDEX IX_Enrollments_PostponedFromEnrollmentId ON dbo.Enrollments(PostponedFromEnrollmentId) WHERE PostponedFromEnrollmentId IS NOT NULL");
            await db.Database.ExecuteSqlRawAsync("IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CreditTransactions_RelatedEnrollmentId_Type' AND object_id = OBJECT_ID(N'dbo.CreditTransactions')) CREATE UNIQUE INDEX IX_CreditTransactions_RelatedEnrollmentId_Type ON dbo.CreditTransactions(RelatedEnrollmentId, Type) WHERE RelatedEnrollmentId IS NOT NULL AND Type IN (6, 7, 8)");
        }
    }

    private static async Task AddSqliteColumn(CoLearnXDbContext db, string table, string column, string type)
    {
        var count = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM pragma_table_info({0}) WHERE name = {1}", table, column).SingleAsync();
        if (count == 0)
        {
            // Identifiers and SQL types are fixed by EnsureAsync.
            var sql = $"ALTER TABLE {table} ADD COLUMN {column} {type}";
            await db.Database.ExecuteSqlRawAsync(sql);
        }
    }

    private static async Task AddSqlServerColumn(CoLearnXDbContext db, string table, string column, string type)
    {
        // Identifiers and SQL types are fixed by EnsureAsync.
        var sql = $"IF COL_LENGTH(N'dbo.{table}', N'{column}') IS NULL ALTER TABLE dbo.{table} ADD {column} {type}";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
