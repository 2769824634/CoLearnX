using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not add tables to a database that already exists.
// Keep this upgrade next to the other schema probes so an older SQLite or SQL Server database receives the pricing tables.
public static class PricingSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            await EnsureSqliteAsync(db, ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.PriceTiers', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.PriceTiers (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PriceTiers PRIMARY KEY,
                        Code nvarchar(1) NOT NULL,
                        TheoryMinHourlyCredits int NOT NULL,
                        TheoryMaxHourlyCredits int NOT NULL,
                        PracticeMinHourlyCredits int NOT NULL,
                        PracticeMaxHourlyCredits int NOT NULL,
                        SortOrder int NOT NULL
                    );
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PriceTiers_Code' AND object_id = OBJECT_ID(N'dbo.PriceTiers'))
                    CREATE UNIQUE INDEX IX_PriceTiers_Code ON dbo.PriceTiers (Code);

                IF OBJECT_ID(N'dbo.CourseDomains', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.CourseDomains (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CourseDomains PRIMARY KEY,
                        Name nvarchar(80) NOT NULL,
                        PriceTierId int NOT NULL,
                        IsActive bit NOT NULL CONSTRAINT DF_CourseDomains_IsActive DEFAULT 1,
                        CreatedAt datetime2 NOT NULL,
                        UpdatedAt datetime2 NOT NULL,
                        CONSTRAINT FK_CourseDomains_PriceTiers_PriceTierId
                            FOREIGN KEY (PriceTierId) REFERENCES dbo.PriceTiers (Id) ON DELETE NO ACTION
                    );
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseDomains_Name' AND object_id = OBJECT_ID(N'dbo.CourseDomains'))
                    CREATE UNIQUE INDEX IX_CourseDomains_Name ON dbo.CourseDomains (Name);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseDomains_PriceTierId' AND object_id = OBJECT_ID(N'dbo.CourseDomains'))
                    CREATE INDEX IX_CourseDomains_PriceTierId ON dbo.CourseDomains (PriceTierId);

                IF OBJECT_ID(N'dbo.PricePools', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.PricePools (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PricePools PRIMARY KEY,
                        SaleMode int NOT NULL,
                        PriceTierId int NULL,
                        CourseType int NOT NULL,
                        CourseLevelId int NOT NULL,
                        MinHourlyCredits int NULL,
                        MaxHourlyCredits int NULL,
                        MinHours int NULL,
                        MaxHours int NULL,
                        MinTotalCredits int NULL,
                        MaxTotalCredits int NULL,
                        ContentSharePercent int NOT NULL,
                        DeliverySharePercent int NOT NULL,
                        PlatformSharePercent int NOT NULL,
                        Subject int NULL,
                        CONSTRAINT FK_PricePools_PriceTiers_PriceTierId
                            FOREIGN KEY (PriceTierId) REFERENCES dbo.PriceTiers (Id) ON DELETE NO ACTION,
                        CONSTRAINT FK_PricePools_CourseLevels_CourseLevelId
                            FOREIGN KEY (CourseLevelId) REFERENCES dbo.CourseLevels (Id) ON DELETE NO ACTION
                    );
                END;
                IF COL_LENGTH(N'dbo.PricePools', N'Subject') IS NULL
                    ALTER TABLE dbo.PricePools ADD Subject int NULL;
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PricePools_SaleMode_PriceTierId_CourseType_CourseLevelId' AND object_id = OBJECT_ID(N'dbo.PricePools'))
                    DROP INDEX IX_PricePools_SaleMode_PriceTierId_CourseType_CourseLevelId ON dbo.PricePools;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PricePools_Subject_CourseLevelId' AND object_id = OBJECT_ID(N'dbo.PricePools'))
                    CREATE UNIQUE INDEX IX_PricePools_Subject_CourseLevelId ON dbo.PricePools (Subject, CourseLevelId);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PricePools_PriceTierId' AND object_id = OBJECT_ID(N'dbo.PricePools'))
                    CREATE INDEX IX_PricePools_PriceTierId ON dbo.PricePools (PriceTierId);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PricePools_CourseLevelId' AND object_id = OBJECT_ID(N'dbo.PricePools'))
                    CREATE INDEX IX_PricePools_CourseLevelId ON dbo.PricePools (CourseLevelId);
                """, ct);
        }
    }

    private static async Task EnsureSqliteAsync(CoLearnXDbContext db, CancellationToken ct)
    {
        if (await MissingAsync(db, "PriceTiers", ct))
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE PriceTiers (
                    Id INTEGER NOT NULL CONSTRAINT PK_PriceTiers PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL,
                    TheoryMinHourlyCredits INTEGER NOT NULL,
                    TheoryMaxHourlyCredits INTEGER NOT NULL,
                    PracticeMinHourlyCredits INTEGER NOT NULL,
                    PracticeMaxHourlyCredits INTEGER NOT NULL,
                    SortOrder INTEGER NOT NULL
                )
                """, ct);
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_PriceTiers_Code ON PriceTiers (Code)
            """, ct);

        if (await MissingAsync(db, "CourseDomains", ct))
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE CourseDomains (
                    Id INTEGER NOT NULL CONSTRAINT PK_CourseDomains PRIMARY KEY AUTOINCREMENT,
                    Name TEXT COLLATE NOCASE NOT NULL,
                    PriceTierId INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    CONSTRAINT FK_CourseDomains_PriceTiers_PriceTierId
                        FOREIGN KEY (PriceTierId) REFERENCES PriceTiers (Id) ON DELETE RESTRICT
                )
                """, ct);
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_CourseDomains_Name ON CourseDomains (Name)
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_CourseDomains_PriceTierId ON CourseDomains (PriceTierId)
            """, ct);

        if (await MissingAsync(db, "PricePools", ct))
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE PricePools (
                    Id INTEGER NOT NULL CONSTRAINT PK_PricePools PRIMARY KEY AUTOINCREMENT,
                    SaleMode INTEGER NOT NULL,
                    PriceTierId INTEGER NULL,
                    CourseType INTEGER NOT NULL,
                    CourseLevelId INTEGER NOT NULL,
                    MinHourlyCredits INTEGER NULL,
                    MaxHourlyCredits INTEGER NULL,
                    MinHours INTEGER NULL,
                    MaxHours INTEGER NULL,
                    MinTotalCredits INTEGER NULL,
                    MaxTotalCredits INTEGER NULL,
                    ContentSharePercent INTEGER NOT NULL,
                    DeliverySharePercent INTEGER NOT NULL,
                    PlatformSharePercent INTEGER NOT NULL,
                    Subject INTEGER NULL,
                    CONSTRAINT FK_PricePools_PriceTiers_PriceTierId
                        FOREIGN KEY (PriceTierId) REFERENCES PriceTiers (Id) ON DELETE RESTRICT,
                    CONSTRAINT FK_PricePools_CourseLevels_CourseLevelId
                        FOREIGN KEY (CourseLevelId) REFERENCES CourseLevels (Id) ON DELETE RESTRICT
                )
                """, ct);
        }

        var subjectColumn = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM pragma_table_info('PricePools') WHERE name = 'Subject'").SingleAsync(ct);
        if (subjectColumn == 0)
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE PricePools ADD COLUMN Subject INTEGER NULL", ct);
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS IX_PricePools_SaleMode_PriceTierId_CourseType_CourseLevelId", ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS IX_PricePools_Subject_CourseLevelId
                ON PricePools (Subject, CourseLevelId)
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_PricePools_PriceTierId ON PricePools (PriceTierId)
            """, ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS IX_PricePools_CourseLevelId ON PricePools (CourseLevelId)
            """, ct);
    }

    private static async Task<bool> MissingAsync(CoLearnXDbContext db, string table, CancellationToken ct)
    {
        var sql = table switch
        {
            "PriceTiers" => "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'PriceTiers'",
            "CourseDomains" => "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'CourseDomains'",
            "PricePools" => "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'PricePools'",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
        return await db.Database.SqlQueryRaw<int>(sql).SingleAsync(ct) == 0;
    }

}
