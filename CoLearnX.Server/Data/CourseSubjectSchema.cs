using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not add columns to a database that already exists.
public static class CourseSubjectSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
            await EnsureSqliteAsync(db, ct);
        else if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.CourseSubjects', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.CourseSubjects (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CourseSubjects PRIMARY KEY,
                        Name nvarchar(80) NOT NULL,
                        Subject int NOT NULL,
                        SortOrder int NOT NULL
                    );
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseSubjects_Name' AND object_id = OBJECT_ID(N'dbo.CourseSubjects'))
                    CREATE UNIQUE INDEX IX_CourseSubjects_Name ON dbo.CourseSubjects (Name);
                IF COL_LENGTH(N'dbo.Courses', N'CourseSubjectId') IS NULL
                    ALTER TABLE dbo.Courses ADD CourseSubjectId int NULL;
                IF COL_LENGTH(N'dbo.Courses', N'DurationHours') IS NULL
                    ALTER TABLE dbo.Courses ADD DurationHours int NULL;
                IF COL_LENGTH(N'dbo.Courses', N'PriceExceptionReason') IS NULL
                    ALTER TABLE dbo.Courses ADD PriceExceptionReason nvarchar(500) NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Courses_CourseSubjects_CourseSubjectId')
                    ALTER TABLE dbo.Courses ADD CONSTRAINT FK_Courses_CourseSubjects_CourseSubjectId
                        FOREIGN KEY (CourseSubjectId) REFERENCES dbo.CourseSubjects (Id);
                """, ct);
    }

    private static async Task EnsureSqliteAsync(CoLearnXDbContext db, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS CourseSubjects (
                Id INTEGER NOT NULL CONSTRAINT PK_CourseSubjects PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Subject INTEGER NOT NULL,
                SortOrder INTEGER NOT NULL
            )
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_CourseSubjects_Name ON CourseSubjects (Name)
            """, ct);
        if (await MissingColumnAsync(db, "Courses", "CourseSubjectId", ct))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Courses ADD COLUMN CourseSubjectId INTEGER NULL", ct);
        if (await MissingColumnAsync(db, "Courses", "DurationHours", ct))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Courses ADD COLUMN DurationHours INTEGER NULL", ct);
        if (await MissingColumnAsync(db, "Courses", "PriceExceptionReason", ct))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Courses ADD COLUMN PriceExceptionReason TEXT NULL", ct);
    }

    private static async Task<bool> MissingColumnAsync(CoLearnXDbContext db, string table, string column, CancellationToken ct)
        => await db.Database.SqlQueryRaw<int>(
            $"SELECT COUNT(*) AS Value FROM pragma_table_info('{table}') WHERE name = '{column}'").SingleAsync(ct) == 0;
}
