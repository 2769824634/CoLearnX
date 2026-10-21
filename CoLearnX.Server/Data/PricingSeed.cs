using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// Reference prices. Insert a missing key only; never overwrite a row an administrator already changed.
public static class PricingSeed
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        var tierRows = new (string Code, int TheoryMin, int TheoryMax, int PracticeMin, int PracticeMax, int Sort)[]
        {
            ("A", 40, 120, 60, 150, 1),
            ("B", 30, 100, 40, 120, 2),
            ("C", 20, 60, 30, 100, 3),
            ("D", 15, 40, 25, 70, 4),
            ("E", 15, 50, 25, 80, 5),
        };
        var existingCodes = (await db.PriceTiers.Select(tier => tier.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tier in tierRows)
        {
            if (existingCodes.Contains(tier.Code)) continue;
            db.PriceTiers.Add(new PriceTier
            {
                Code = tier.Code,
                TheoryMinHourlyCredits = tier.TheoryMin,
                TheoryMaxHourlyCredits = tier.TheoryMax,
                PracticeMinHourlyCredits = tier.PracticeMin,
                PracticeMaxHourlyCredits = tier.PracticeMax,
                SortOrder = tier.Sort,
            });
        }

        await db.SaveChangesAsync(ct);
        var tiers = await db.PriceTiers.ToDictionaryAsync(tier => tier.Code, StringComparer.OrdinalIgnoreCase, ct);
        var domainRows = new (string Name, string Tier)[]
        {
            ("Business and Management", "A"),
            ("Education and Career", "A"),
            ("IT and Data", "B"),
            ("Design and Media", "B"),
            ("Health and Care", "C"),
            ("Household and Hospitality", "C"),
            ("Lifestyle and Crafts", "D"),
            ("Languages and Arts", "E"),
        };
        var existingNames = (await db.CourseDomains.Select(domain => domain.Name).ToListAsync(ct))
            .Select(name => name.ToLowerInvariant())
            .ToHashSet();
        var now = DateTime.UtcNow;
        foreach (var domain in domainRows)
        {
            if (existingNames.Contains(domain.Name.ToLowerInvariant())) continue;
            db.CourseDomains.Add(new CourseDomain
            {
                Name = domain.Name,
                PriceTierId = tiers[domain.Tier].Id,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync(ct);
        var levels = await db.CourseLevels.ToDictionaryAsync(level => level.Name, level => level.Id, ct);
        var existingCells = (await db.PricePools
                .Select(row => new { row.SaleMode, row.PriceTierId, row.CourseType, row.CourseLevelId })
                .ToListAsync(ct))
            .Select(row => Key(row.SaleMode, row.PriceTierId, row.CourseType, row.CourseLevelId))
            .ToHashSet();

        foreach (var tier in tiers.Values)
        {
            foreach (var type in new[] { CourseType.Theory, CourseType.Practice })
            {
                var low = type == CourseType.Theory ? tier.TheoryMinHourlyCredits : tier.PracticeMinHourlyCredits;
                var high = type == CourseType.Theory ? tier.TheoryMaxHourlyCredits : tier.PracticeMaxHourlyCredits;
                var mid = (low + high + 1) / 2;
                foreach (var level in new[] { "Beginner", "Intermediate", "Advanced" })
                {
                    var levelId = levels[level];
                    if (!existingCells.Add(Key(SaleMode.Intake, tier.Id, type, levelId))) continue;
                    var (hourlyMin, hourlyMax) = level switch
                    {
                        "Beginner" => (low, mid),
                        "Advanced" => (mid, high),
                        _ => (low, high),
                    };
                    var (hourMin, hourMax) = Hours(type, level);
                    var (content, delivery, platform) = type == CourseType.Theory ? (25, 25, 50) : (10, 45, 45);
                    db.PricePools.Add(new PricePool
                    {
                        SaleMode = SaleMode.Intake,
                        PriceTierId = tier.Id,
                        CourseType = type,
                        CourseLevelId = levelId,
                        MinHourlyCredits = hourlyMin,
                        MaxHourlyCredits = hourlyMax,
                        MinHours = hourMin,
                        MaxHours = hourMax,
                        ContentSharePercent = content,
                        DeliverySharePercent = delivery,
                        PlatformSharePercent = platform,
                    });
                }
            }
        }

        foreach (var (level, totalMin, totalMax) in new[] { ("Beginner", 10, 100), ("Intermediate", 50, 400), ("Advanced", 150, 1000) })
        {
            var levelId = levels[level];
            if (!existingCells.Add(Key(SaleMode.CoursePack, null, CourseType.Theory, levelId))) continue;
            db.PricePools.Add(new PricePool
            {
                SaleMode = SaleMode.CoursePack,
                PriceTierId = null,
                CourseType = CourseType.Theory,
                CourseLevelId = levelId,
                MinTotalCredits = totalMin,
                MaxTotalCredits = totalMax,
                ContentSharePercent = 50,
                DeliverySharePercent = 0,
                PlatformSharePercent = 50,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static string Key(SaleMode mode, int? tierId, CourseType type, int levelId)
        => $"{mode}|{tierId}|{type}|{levelId}";

    private static (int Min, int Max) Hours(CourseType type, string level) => (type, level) switch
    {
        (CourseType.Theory, "Beginner") => (2, 12),
        (CourseType.Theory, "Intermediate") => (12, 40),
        (CourseType.Theory, "Advanced") => (24, 80),
        (CourseType.Practice, "Beginner") => (2, 16),
        (CourseType.Practice, "Intermediate") => (16, 60),
        (CourseType.Practice, "Advanced") => (40, 200),
        _ => throw new InvalidOperationException($"No hour band for {type} {level}."),
    };
}
