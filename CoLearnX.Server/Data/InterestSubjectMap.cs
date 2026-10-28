using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Data;

// Search tags do not set the price. They must sit in the same band as the chosen subject.
public static class InterestSubjectMap
{
    public static SubjectArea? For(string? parentSlug, string? leafSlug)
    {
        if (parentSlug == "design")
        {
            return leafSlug is "user-experience-design" or "ui-visual-design" or "ux-research" or "web-design" or "design-systems"
                ? SubjectArea.DigitalTechnology
                : SubjectArea.HumanitiesHealthCreative;
        }

        return parentSlug switch
        {
            "development" or "it-software" or "data-ai" => SubjectArea.DigitalTechnology,
            "business" or "finance-accounting" or "marketing" or "office-productivity" => SubjectArea.BusinessManagement,
            "art" or "photography-video" or "health-fitness" or "music" or "language-learning" or "personal-development" or "teaching-academics" => SubjectArea.HumanitiesHealthCreative,
            _ => null,
        };
    }
}
