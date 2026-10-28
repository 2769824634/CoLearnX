using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public sealed class PricingCatalogTests
{
    [Fact]
    public async Task Seed_creates_nine_company_price_cells_and_a_second_run_does_not_duplicate_them()
    {
        var (connection, db) = await OpenAsync();
        await using var connectionScope = connection;
        await using var dbScope = db;
        await SeedData.InitializeAsync(db, seedDemoAdmin: false);

        Assert.Equal(9, await db.PricePools.CountAsync());
        Assert.Equal(0, await db.PriceTiers.CountAsync());
        Assert.Equal(0, await db.CourseDomains.CountAsync());

        var cells = await db.PricePools.AsNoTracking().Include(row => row.CourseLevel).ToListAsync();
        foreach (var (subject, level, min, max) in Cells)
        {
            var row = cells.Single(item => item.Subject == subject && item.CourseLevel.Name == level);
            Assert.Equal(min, row.MinTotalCredits);
            Assert.Equal(max, row.MaxTotalCredits);
            Assert.Null(row.MinHourlyCredits);
            Assert.Null(row.MaxHourlyCredits);
            Assert.Null(row.MinHours);
            Assert.Null(row.MaxHours);
            Assert.Equal((25, 25, 50), (row.ContentSharePercent, row.DeliverySharePercent, row.PlatformSharePercent));
        }

        await SeedData.InitializeAsync(db, seedDemoAdmin: false);
        Assert.Equal(9, await db.PricePools.CountAsync());
    }

    private static async Task<(SqliteConnection Connection, CoLearnXDbContext Db)> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        return (connection, db);
    }

    private static readonly (SubjectArea Subject, string Level, int Min, int Max)[] Cells =
    [
        (SubjectArea.DigitalTechnology, "Beginner", 40, 180),
        (SubjectArea.DigitalTechnology, "Intermediate", 120, 400),
        (SubjectArea.DigitalTechnology, "Advanced", 300, 800),
        (SubjectArea.BusinessManagement, "Beginner", 50, 220),
        (SubjectArea.BusinessManagement, "Intermediate", 150, 500),
        (SubjectArea.BusinessManagement, "Advanced", 350, 900),
        (SubjectArea.HumanitiesHealthCreative, "Beginner", 20, 120),
        (SubjectArea.HumanitiesHealthCreative, "Intermediate", 60, 280),
        (SubjectArea.HumanitiesHealthCreative, "Advanced", 150, 500),
    ];
}
