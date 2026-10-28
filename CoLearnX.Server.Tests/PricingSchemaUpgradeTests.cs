using CoLearnX.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public sealed class PricingSchemaUpgradeTests
{
    [Fact]
    public async Task Existing_sqlite_database_gains_pricing_tables_matching_a_new_database()
    {
        var (upgradedConnection, upgraded) = await OpenAsync();
        await using var upgradedScope = upgradedConnection;
        await using var upgradedDb = upgraded;
        await upgraded.Database.ExecuteSqlRawAsync("""
            CREATE TABLE CourseLevels (
                Id INTEGER NOT NULL CONSTRAINT PK_CourseLevels PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL
            );
            INSERT INTO CourseLevels (Id, Name, SortOrder) VALUES (1, 'Beginner', 1);
            """);

        await PricingSchema.EnsureAsync(upgraded);
        await upgraded.Database.ExecuteSqlRawAsync("""
            INSERT INTO PriceTiers (Code, TheoryMinHourlyCredits, TheoryMaxHourlyCredits, PracticeMinHourlyCredits, PracticeMaxHourlyCredits, SortOrder)
            VALUES ('A', 40, 120, 60, 150, 1);
            INSERT INTO CourseDomains (Name, PriceTierId, IsActive, CreatedAt, UpdatedAt)
            VALUES ('Languages and Arts', 1, 1, '2026-10-19T00:00:00Z', '2026-10-19T00:00:00Z');
            INSERT INTO PricePools (SaleMode, PriceTierId, CourseType, CourseLevelId, MinHourlyCredits, MaxHourlyCredits, MinHours, MaxHours, MinTotalCredits, MaxTotalCredits, ContentSharePercent, DeliverySharePercent, PlatformSharePercent)
            VALUES (0, 1, 0, 1, 40, 80, 2, 12, NULL, NULL, 25, 25, 50);
            """);
        await PricingSchema.EnsureAsync(upgraded);

        Assert.Equal(1, await CountAsync(upgraded, "PriceTiers"));
        Assert.Equal(1, await CountAsync(upgraded, "CourseDomains"));
        Assert.Equal(1, await CountAsync(upgraded, "PricePools"));

        var (createdConnection, created) = await OpenAsync();
        await using var createdScope = createdConnection;
        await using var createdDb = created;
        await created.Database.EnsureCreatedAsync();

        foreach (var table in new[] { "PriceTiers", "CourseDomains", "PricePools" })
        {
            Assert.Equal(await SignatureAsync(created, table, "pragma_table_info"), await SignatureAsync(upgraded, table, "pragma_table_info"));
            Assert.Equal(await SignatureAsync(created, table, "pragma_index_list"), await SignatureAsync(upgraded, table, "pragma_index_list"));
            Assert.Equal(await SignatureAsync(created, table, "pragma_foreign_key_list"), await SignatureAsync(upgraded, table, "pragma_foreign_key_list"));
        }

        Assert.Contains("IX_PriceTiers_Code|1", await SignatureAsync(created, "PriceTiers", "pragma_index_list"));
        Assert.Contains("IX_CourseDomains_Name|1", await SignatureAsync(created, "CourseDomains", "pragma_index_list"));
        Assert.Contains("IX_PricePools_Subject_CourseLevelId", await SignatureAsync(created, "PricePools", "pragma_index_list"));
    }

    private static async Task<(SqliteConnection Connection, CoLearnXDbContext Db)> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        return (connection, db);
    }

    private static async Task<int> CountAsync(CoLearnXDbContext db, string table)
    {
        var sql = table switch
        {
            "PriceTiers" => "SELECT COUNT(*) AS Value FROM PriceTiers",
            "CourseDomains" => "SELECT COUNT(*) AS Value FROM CourseDomains",
            "PricePools" => "SELECT COUNT(*) AS Value FROM PricePools",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
        return await db.Database.SqlQueryRaw<int>(sql).SingleAsync();
    }

    private static async Task<string> SignatureAsync(CoLearnXDbContext db, string table, string pragma)
    {
        var sql = (table, pragma) switch
        {
            ("PriceTiers", "pragma_table_info") => "SELECT name || '|' || type || '|' || \"notnull\" || '|' || IFNULL(dflt_value, '') || '|' || pk AS Value FROM pragma_table_info('PriceTiers') ORDER BY cid",
            ("CourseDomains", "pragma_table_info") => "SELECT name || '|' || type || '|' || \"notnull\" || '|' || IFNULL(dflt_value, '') || '|' || pk AS Value FROM pragma_table_info('CourseDomains') ORDER BY cid",
            ("PricePools", "pragma_table_info") => "SELECT name || '|' || type || '|' || \"notnull\" || '|' || IFNULL(dflt_value, '') || '|' || pk AS Value FROM pragma_table_info('PricePools') ORDER BY cid",
            ("PriceTiers", "pragma_index_list") => "SELECT name || '|' || \"unique\" AS Value FROM pragma_index_list('PriceTiers') ORDER BY name",
            ("CourseDomains", "pragma_index_list") => "SELECT name || '|' || \"unique\" AS Value FROM pragma_index_list('CourseDomains') ORDER BY name",
            ("PricePools", "pragma_index_list") => "SELECT name || '|' || \"unique\" AS Value FROM pragma_index_list('PricePools') ORDER BY name",
            ("PriceTiers", "pragma_foreign_key_list") => "SELECT \"table\" || '|' || \"from\" || '|' || \"to\" || '|' || on_delete AS Value FROM pragma_foreign_key_list('PriceTiers') ORDER BY \"table\", \"from\"",
            ("CourseDomains", "pragma_foreign_key_list") => "SELECT \"table\" || '|' || \"from\" || '|' || \"to\" || '|' || on_delete AS Value FROM pragma_foreign_key_list('CourseDomains') ORDER BY \"table\", \"from\"",
            ("PricePools", "pragma_foreign_key_list") => "SELECT \"table\" || '|' || \"from\" || '|' || \"to\" || '|' || on_delete AS Value FROM pragma_foreign_key_list('PricePools') ORDER BY \"table\", \"from\"",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
        var rows = await db.Database.SqlQueryRaw<string>(sql).ToListAsync();
        return string.Join("\n", rows);
    }
}
