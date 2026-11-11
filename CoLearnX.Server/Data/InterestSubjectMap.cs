using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// Maps an onboarding interest to one of the 12 course subjects, which are also the Trainer and Creator specialties.
// Search tags do not set the price; a course's tags must sit in the same area as its chosen subject.
public static class InterestSubjectMap
{
    private static readonly Dictionary<string, SubjectArea> Areas = new()
    {
        ["Software Development"] = SubjectArea.DigitalTechnology,
        ["Data and Artificial Intelligence"] = SubjectArea.DigitalTechnology,
        ["Cybersecurity and Infrastructure"] = SubjectArea.DigitalTechnology,
        ["Digital Product Design"] = SubjectArea.DigitalTechnology,
        ["Management"] = SubjectArea.BusinessManagement,
        ["Accounting and Finance"] = SubjectArea.BusinessManagement,
        ["Marketing"] = SubjectArea.BusinessManagement,
        ["Organizational Operations"] = SubjectArea.BusinessManagement,
        ["Languages"] = SubjectArea.HumanitiesHealthCreative,
        ["Education and Academic Foundations"] = SubjectArea.HumanitiesHealthCreative,
        ["Health and Care"] = SubjectArea.HumanitiesHealthCreative,
        ["Media Production"] = SubjectArea.HumanitiesHealthCreative,
    };

    public static string? SubjectFor(string? parentSlug, string? leafSlug) => parentSlug switch
    {
        "development" => "Software Development",
        "it-software" => "Cybersecurity and Infrastructure",
        "data-ai" => "Data and Artificial Intelligence",
        "design" => leafSlug is "user-experience-design" or "ui-visual-design" or "ux-research" or "web-design" or "design-systems"
            ? "Digital Product Design"
            : "Media Production",
        "art" or "photography-video" or "music" => "Media Production",
        "business" => leafSlug switch
        {
            "sales" => "Marketing",
            "operations" or "e-commerce" => "Organizational Operations",
            _ => "Management",
        },
        "finance-accounting" => "Accounting and Finance",
        "marketing" => "Marketing",
        "office-productivity" => "Organizational Operations",
        "personal-development" => leafSlug == "stress-management" ? "Health and Care" : "Education and Academic Foundations",
        "health-fitness" => "Health and Care",
        "language-learning" => "Languages",
        "teaching-academics" => "Education and Academic Foundations",
        _ => null,
    };

    public static SubjectArea? For(string? parentSlug, string? leafSlug)
        => SubjectFor(parentSlug, leafSlug) is { } subject ? Areas[subject] : null;

    // The course subject ids a Member's interests point to.
    public static async Task<HashSet<int>> SubjectIdsForInterestsAsync(CoLearnXDbContext db, IReadOnlyCollection<int> interestIds, CancellationToken ct)
    {
        if (interestIds.Count == 0) return [];
        var interests = await db.Interests.AsNoTracking().Include(item => item.Parent)
            .Where(item => interestIds.Contains(item.Id)).ToListAsync(ct);
        var names = interests.Select(item => SubjectFor(item.Parent?.Slug, item.Slug)).OfType<string>().ToHashSet();
        return (await db.CourseSubjects.AsNoTracking().Where(item => names.Contains(item.Name))
            .Select(item => item.Id).ToListAsync(ct)).ToHashSet();
    }
}
