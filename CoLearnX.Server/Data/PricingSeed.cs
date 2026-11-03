using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// Company price fence. Nine cells: three subjects by three levels. Do not overwrite a cell that is already present.
public static class PricingSeed
{
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

    private static readonly (string Name, SubjectArea Subject)[] Subjects =
    [
        ("Software Development", SubjectArea.DigitalTechnology),
        ("Data and Artificial Intelligence", SubjectArea.DigitalTechnology),
        ("Cybersecurity and Infrastructure", SubjectArea.DigitalTechnology),
        ("Digital Product Design", SubjectArea.DigitalTechnology),
        ("Management", SubjectArea.BusinessManagement),
        ("Accounting and Finance", SubjectArea.BusinessManagement),
        ("Marketing", SubjectArea.BusinessManagement),
        ("Organizational Operations", SubjectArea.BusinessManagement),
        ("Languages", SubjectArea.HumanitiesHealthCreative),
        ("Education and Academic Foundations", SubjectArea.HumanitiesHealthCreative),
        ("Health and Care", SubjectArea.HumanitiesHealthCreative),
        ("Media Production", SubjectArea.HumanitiesHealthCreative),
    ];

    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        var names = (await db.CourseSubjects.Select(subject => subject.Name).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < Subjects.Length; index++)
        {
            if (names.Contains(Subjects[index].Name)) continue;
            db.CourseSubjects.Add(new CourseSubject
            {
                Name = Subjects[index].Name,
                Subject = Subjects[index].Subject,
                SortOrder = index + 1,
            });
        }

        await db.SaveChangesAsync(ct);

        var obsolete = await db.PricePools.Where(row => row.Subject == null).ToListAsync(ct);
        if (obsolete.Count > 0)
        {
            db.PricePools.RemoveRange(obsolete);
            await db.SaveChangesAsync(ct);
        }

        var levels = await db.CourseLevels.ToDictionaryAsync(level => level.Name, level => level.Id, ct);
        var rows = await db.PricePools.Where(row => row.Subject != null).ToListAsync(ct);

        foreach (var cell in Cells)
        {
            var levelId = levels[cell.Level];
            var (minHours, maxHours) = HoursFor(cell.Level);
            var row = rows.SingleOrDefault(item => item.Subject == cell.Subject && item.CourseLevelId == levelId);
            if (row is null)
            {
                db.PricePools.Add(new PricePool
                {
                    SaleMode = SaleMode.CoursePack,
                    CourseType = CourseType.Theory,
                    CourseLevelId = levelId,
                    Subject = cell.Subject,
                    MinTotalCredits = cell.Min,
                    MaxTotalCredits = cell.Max,
                    MinHours = minHours,
                    MaxHours = maxHours,
                    ContentSharePercent = 25,
                    DeliverySharePercent = 25,
                    PlatformSharePercent = 50,
                });
                continue;
            }
            row.MinHours = minHours;
            row.MaxHours = maxHours;
        }

        await db.SaveChangesAsync(ct);
    }

    private static (int Min, int Max) HoursFor(string level) => level switch
    {
        "Intermediate" => (12, 40),
        "Advanced" => (20, 80),
        _ => (4, 16),
    };
}
