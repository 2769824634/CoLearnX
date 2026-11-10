using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IMedalService
{
    Task<IReadOnlyList<MedalDto>> ListForMemberAsync(int userId, CancellationToken ct = default);
}

public sealed record MedalStanding(string Material, string Requirement, string Progress);

// One hexagonal medal per subject area. Each finished course in the area counts once.
public static class MedalRanks
{
    private const string FullRule =
        "Finish any course in this area to earn Brass. Three or more courses at Intermediate or Advanced reach Silver. Five or more courses, including an Advanced course, reach Gold. Five or more courses across Beginner, Intermediate, and Advanced reach Diamond.";

    public static MedalStanding Describe(IEnumerable<string>? levelNames)
    {
        var courses = (levelNames ?? []).ToList();
        var count = courses.Count;
        var beginner = Count(courses, "Beginner");
        var intermediate = Count(courses, "Intermediate");
        var advanced = Count(courses, "Advanced");
        var higher = intermediate >= 1 || advanced >= 1;
        var diamond = count >= 5 && beginner >= 1 && intermediate >= 1 && advanced >= 1;
        var gold = count >= 5 && advanced >= 1;
        var silver = count >= 3 && higher;

        if (diamond)
            return new MedalStanding("Diamond",
                "Highest level. You finished at least 5 courses in this area, including a Beginner, an Intermediate, and an Advanced course.",
                $"{count} courses across Beginner, Intermediate, and Advanced.");
        if (gold)
        {
            var missing = MissingLevels(beginner, intermediate);
            return new MedalStanding("Gold",
                $"Finish {missing} in this area to reach Diamond.",
                $"{count} courses. Still need {missing}.");
        }
        if (silver)
        {
            if (advanced == 0)
            {
                var requirement = count >= 5
                    ? "Finish an Advanced course in this area to reach Gold."
                    : "Finish at least 5 courses in this area, including an Advanced course, to reach Gold.";
                var progress = count >= 5
                    ? $"{count} courses. Still need an Advanced course."
                    : $"{count}/5 courses. Still need an Advanced course.";
                return new MedalStanding("Silver", requirement, progress);
            }

            var reachesDiamond = beginner >= 1 && intermediate >= 1;
            return new MedalStanding("Silver",
                reachesDiamond
                    ? "Finish at least 5 courses in this area to reach Diamond."
                    : "Finish at least 5 courses in this area to reach Gold.",
                $"{count}/5 courses.");
        }
        if (count >= 1)
        {
            if (!higher)
            {
                var requirement = count >= 3
                    ? "Finish an Intermediate or Advanced course in this area to reach Silver."
                    : "Finish at least 3 courses in this area, including an Intermediate or Advanced course, to reach Silver.";
                var progress = count >= 3
                    ? $"{count} courses. Still need an Intermediate or Advanced course."
                    : $"{count}/3 courses. Still need an Intermediate or Advanced course.";
                return new MedalStanding("Brass", requirement, progress);
            }
            return new MedalStanding("Brass", "Finish at least 3 courses in this area to reach Silver.", $"{count}/3 courses.");
        }
        return new MedalStanding("Locked", FullRule, "0/1 courses.");
    }

    private static string MissingLevels(int beginner, int intermediate)
    {
        var missing = new List<string>();
        if (beginner < 1) missing.Add("a Beginner course");
        if (intermediate < 1) missing.Add("an Intermediate course");
        return missing.Count == 2 ? $"{missing[0]} and {missing[1]}" : missing[0];
    }

    private static int Count(IEnumerable<string> courses, string level)
        => courses.Count(name => string.Equals(name, level, StringComparison.OrdinalIgnoreCase));
}

public class MedalService(CoLearnXDbContext db) : IMedalService
{
    private static readonly (SubjectArea Area, string Name)[] Catalog =
    [
        (SubjectArea.DigitalTechnology, "Digital Technology"),
        (SubjectArea.BusinessManagement, "Business and Management"),
        (SubjectArea.HumanitiesHealthCreative, "Humanities, Health and Creative"),
    ];

    public async Task<IReadOnlyList<MedalDto>> ListForMemberAsync(int userId, CancellationToken ct = default)
    {
        var completed = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.UserId == userId
                && enrollment.Status == EnrollmentStatus.Completed
                && enrollment.Course.CourseSubjectId != null)
            .Select(enrollment => new
            {
                enrollment.CourseId,
                enrollment.Course.CourseSubject!.Subject,
                LevelName = enrollment.Course.CourseLevel != null
                    ? enrollment.Course.CourseLevel.Name
                    : enrollment.Course.Level,
            })
            .ToListAsync(ct);

        var byArea = completed
            .GroupBy(row => row.Subject)
            .ToDictionary(
                group => group.Key,
                group => (IEnumerable<string>)group
                    .GroupBy(row => row.CourseId)
                    .Select(course => course.Select(row => row.LevelName).FirstOrDefault() ?? ""));

        return Catalog.Select(item =>
        {
            byArea.TryGetValue(item.Area, out var levels);
            var standing = MedalRanks.Describe(levels);
            return new MedalDto(item.Area.ToString(), item.Name, standing.Material, standing.Requirement, standing.Progress);
        }).ToList();
    }
}
