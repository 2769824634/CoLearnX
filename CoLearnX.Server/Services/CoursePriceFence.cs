using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

internal static class CoursePriceFence
{
    public static async Task<(CourseSubject Subject, string? ExceptionReason)> RequireAsync(
        CoLearnXDbContext db,
        int courseSubjectId,
        CourseLevel level,
        int durationHours,
        int creditCost,
        string? exceptionReason,
        CancellationToken ct)
    {
        if (durationHours < 1)
            throw new CourseException("DURATION_HOURS_INVALID", "Enter a whole number of hours, at least 1.", 400, "durationHours");
        var subject = await db.CourseSubjects.SingleOrDefaultAsync(item => item.Id == courseSubjectId, ct)
            ?? throw new CourseException("COURSE_SUBJECT_NOT_FOUND", "Select a subject.", 400, "courseSubjectId");
        var cell = await db.PricePools.AsNoTracking().SingleOrDefaultAsync(row => row.Subject == subject.Subject && row.CourseLevelId == level.Id, ct);
        if (cell?.MinTotalCredits is not int min || cell.MaxTotalCredits is not int max || min > max)
            throw new CourseException("PRICE_POOL_UNAVAILABLE", $"{subject.Name} prices cannot be saved right now.", 409, "creditCost");
        if (creditCost > max * 2)
            throw new CourseException("CREDIT_COST_ABOVE_CAP", $"Price cannot be more than {max * 2} credits.", 400, "creditCost");
        if (creditCost < min || creditCost > max)
        {
            if (creditCost < min)
                throw new CourseException("CREDIT_COST_OUT_OF_RANGE", $"Enter a whole number from {min} to {max} credits.", 400, "creditCost");
            var reason = exceptionReason?.Trim();
            if (string.IsNullOrEmpty(reason))
                throw new CourseException("PRICE_EXCEPTION_REASON_REQUIRED", "Explain why this price is above the range.", 400, "priceExceptionReason");
            return (subject, reason.Length > 500 ? reason[..500] : reason);
        }
        return (subject, null);
    }
}
