using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public sealed class PricingCatalogTests
{
    [Fact]
    public async Task Seed_creates_the_price_catalog_and_a_second_run_keeps_admin_edits()
    {
        var (connection, db) = await OpenAsync();
        await using var connectionScope = connection;
        await using var dbScope = db;
        await SeedData.InitializeAsync(db, seedDemoAdmin: false);

        Assert.Equal(5, await db.PriceTiers.CountAsync());
        Assert.Equal(8, await db.CourseDomains.CountAsync());
        Assert.Equal(30, await db.PricePools.CountAsync(row => row.SaleMode == SaleMode.Intake));
        Assert.Equal(3, await db.PricePools.CountAsync(row => row.SaleMode == SaleMode.CoursePack));

        var tiers = await db.PriceTiers.AsNoTracking().ToDictionaryAsync(tier => tier.Code);
        Assert.Equal((40, 120, 60, 150, 1), (tiers["A"].TheoryMinHourlyCredits, tiers["A"].TheoryMaxHourlyCredits, tiers["A"].PracticeMinHourlyCredits, tiers["A"].PracticeMaxHourlyCredits, tiers["A"].SortOrder));
        Assert.Equal((15, 50, 25, 80, 5), (tiers["E"].TheoryMinHourlyCredits, tiers["E"].TheoryMaxHourlyCredits, tiers["E"].PracticeMinHourlyCredits, tiers["E"].PracticeMaxHourlyCredits, tiers["E"].SortOrder));

        var domains = await db.CourseDomains.AsNoTracking().Include(domain => domain.PriceTier).ToListAsync();
        Assert.Equal("A", domains.Single(domain => domain.Name == "Business and Management").PriceTier.Code);
        Assert.Equal("A", domains.Single(domain => domain.Name == "Education and Career").PriceTier.Code);
        Assert.Equal("B", domains.Single(domain => domain.Name == "IT and Data").PriceTier.Code);
        Assert.Equal("B", domains.Single(domain => domain.Name == "Design and Media").PriceTier.Code);
        Assert.Equal("C", domains.Single(domain => domain.Name == "Health and Care").PriceTier.Code);
        Assert.Equal("C", domains.Single(domain => domain.Name == "Household and Hospitality").PriceTier.Code);
        Assert.Equal("D", domains.Single(domain => domain.Name == "Lifestyle and Crafts").PriceTier.Code);
        Assert.Equal("E", domains.Single(domain => domain.Name == "Languages and Arts").PriceTier.Code);

        var cells = await db.PricePools.AsNoTracking().Include(row => row.PriceTier).Include(row => row.CourseLevel).ToListAsync();
        Assert.All(cells, row => Assert.Equal(100, row.ContentSharePercent + row.DeliverySharePercent + row.PlatformSharePercent));

        foreach (var (tier, type, level, hourlyMin, hourlyMax) in IntakeCells)
        {
            var row = cells.Single(item => item.SaleMode == SaleMode.Intake && item.PriceTier!.Code == tier && item.CourseType == type && item.CourseLevel.Name == level);
            var (hourMin, hourMax) = Hours(type, level);
            var (content, delivery, platform) = type == CourseType.Theory ? (25, 25, 50) : (10, 45, 45);
            Assert.Equal(hourlyMin, row.MinHourlyCredits);
            Assert.Equal(hourlyMax, row.MaxHourlyCredits);
            Assert.Equal(hourMin, row.MinHours);
            Assert.Equal(hourMax, row.MaxHours);
            Assert.Null(row.MinTotalCredits);
            Assert.Null(row.MaxTotalCredits);
            Assert.Equal((content, delivery, platform), (row.ContentSharePercent, row.DeliverySharePercent, row.PlatformSharePercent));
        }

        foreach (var (level, totalMin, totalMax) in new[] { ("Beginner", 10, 100), ("Intermediate", 50, 400), ("Advanced", 150, 1000) })
        {
            var row = cells.Single(item => item.SaleMode == SaleMode.CoursePack && item.CourseLevel.Name == level);
            Assert.Null(row.PriceTierId);
            Assert.Equal(CourseType.Theory, row.CourseType);
            Assert.Null(row.MinHourlyCredits);
            Assert.Null(row.MaxHourlyCredits);
            Assert.Null(row.MinHours);
            Assert.Null(row.MaxHours);
            Assert.Equal(totalMin, row.MinTotalCredits);
            Assert.Equal(totalMax, row.MaxTotalCredits);
            Assert.Equal((50, 0, 50), (row.ContentSharePercent, row.DeliverySharePercent, row.PlatformSharePercent));
        }

        var edited = await db.CourseDomains.SingleAsync(domain => domain.Name == "IT and Data");
        edited.Name = "Information Technology";
        edited.PriceTierId = tiers["A"].Id;
        var priced = await db.PricePools.SingleAsync(row => row.SaleMode == SaleMode.Intake && row.PriceTierId == tiers["A"].Id && row.CourseType == CourseType.Theory && row.CourseLevel.Name == "Beginner");
        priced.MinHourlyCredits = 41;
        await db.SaveChangesAsync();

        await SeedData.InitializeAsync(db, seedDemoAdmin: false);

        Assert.Equal(5, await db.PriceTiers.CountAsync());
        Assert.Equal(33, await db.PricePools.CountAsync());
        var kept = await db.CourseDomains.AsNoTracking().Include(domain => domain.PriceTier).SingleAsync(domain => domain.Id == edited.Id);
        Assert.Equal("Information Technology", kept.Name);
        Assert.Equal("A", kept.PriceTier.Code);
        Assert.Equal(41, await db.PricePools.Where(row => row.Id == priced.Id).Select(row => row.MinHourlyCredits).SingleAsync());
        Assert.Equal(1, await db.CourseDomains.CountAsync(domain => domain.Name == "Information Technology"));
    }

    private static async Task<(SqliteConnection Connection, CoLearnXDbContext Db)> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        return (connection, db);
    }

    private static (int Min, int Max) Hours(CourseType type, string level) => (type, level) switch
    {
        (CourseType.Theory, "Beginner") => (2, 12),
        (CourseType.Theory, "Intermediate") => (12, 40),
        (CourseType.Theory, "Advanced") => (24, 80),
        (CourseType.Practice, "Beginner") => (2, 16),
        (CourseType.Practice, "Intermediate") => (16, 60),
        (CourseType.Practice, "Advanced") => (40, 200),
        _ => throw new ArgumentOutOfRangeException(nameof(level)),
    };

    private static readonly (string Tier, CourseType Type, string Level, int Min, int Max)[] IntakeCells =
    [
        ("A", CourseType.Theory, "Beginner", 40, 80),
        ("A", CourseType.Theory, "Intermediate", 40, 120),
        ("A", CourseType.Theory, "Advanced", 80, 120),
        ("A", CourseType.Practice, "Beginner", 60, 105),
        ("A", CourseType.Practice, "Intermediate", 60, 150),
        ("A", CourseType.Practice, "Advanced", 105, 150),
        ("B", CourseType.Theory, "Beginner", 30, 65),
        ("B", CourseType.Theory, "Intermediate", 30, 100),
        ("B", CourseType.Theory, "Advanced", 65, 100),
        ("B", CourseType.Practice, "Beginner", 40, 80),
        ("B", CourseType.Practice, "Intermediate", 40, 120),
        ("B", CourseType.Practice, "Advanced", 80, 120),
        ("C", CourseType.Theory, "Beginner", 20, 40),
        ("C", CourseType.Theory, "Intermediate", 20, 60),
        ("C", CourseType.Theory, "Advanced", 40, 60),
        ("C", CourseType.Practice, "Beginner", 30, 65),
        ("C", CourseType.Practice, "Intermediate", 30, 100),
        ("C", CourseType.Practice, "Advanced", 65, 100),
        ("D", CourseType.Theory, "Beginner", 15, 28),
        ("D", CourseType.Theory, "Intermediate", 15, 40),
        ("D", CourseType.Theory, "Advanced", 28, 40),
        ("D", CourseType.Practice, "Beginner", 25, 48),
        ("D", CourseType.Practice, "Intermediate", 25, 70),
        ("D", CourseType.Practice, "Advanced", 48, 70),
        ("E", CourseType.Theory, "Beginner", 15, 33),
        ("E", CourseType.Theory, "Intermediate", 15, 50),
        ("E", CourseType.Theory, "Advanced", 33, 50),
        ("E", CourseType.Practice, "Beginner", 25, 53),
        ("E", CourseType.Practice, "Intermediate", 25, 80),
        ("E", CourseType.Practice, "Advanced", 53, 80),
    ];
}
