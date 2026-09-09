using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;

namespace CoLearnX.Server.Services;

public sealed class CourseIntakeException(string code, string message, int statusCode = 400, string? field = null) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; } = field is null
        ? new Dictionary<string, string[]>() : new Dictionary<string, string[]> { [field] = [message] };
}

internal static class CourseIntakeValidation
{
    public static void Utc(DateTime value, string field)
    {
        if (value == default || value.Kind != DateTimeKind.Utc)
            throw new CourseIntakeException("INVALID_UTC_TIME", "Provide a UTC timestamp with a Z suffix.", field: field);
    }

    public static void Dates(DateTime opens, DateTime closes, DateTime starts, DateTime ends)
    {
        if (!(opens < closes && closes <= starts && starts < ends))
            throw new CourseIntakeException("INVALID_INTAKE_DATES", "Registration opens < closes <= delivery starts < ends is required.", field: "registrationClosesAt");
    }

    public static void Session(CourseSession session, DateTime starts, DateTime ends)
    {
        if (string.IsNullOrWhiteSpace(session.Label) || session.Label.Length > 128)
            throw new CourseIntakeException("INVALID_SESSION_LABEL", "A session label of up to 128 characters is required.", field: "label");
        if (session.StartsAt >= session.EndsAt || session.StartsAt < starts || session.EndsAt > ends)
            throw new CourseIntakeException("SESSION_OUTSIDE_INTAKE", "The session must be within the Intake delivery period.", field: "startsAt");
        if (session.MeetingLink is not null && (session.MeetingLink.Length > 2048
            || !Uri.TryCreate(session.MeetingLink, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
            throw new CourseIntakeException("INVALID_MEETING_LINK", "Use an absolute HTTP or HTTPS meeting link.", field: "meetingLink");
        var physical = session.PhysicalAddress is not null;
        if (session.PhysicalCapacity < 0 || session.PhysicalAddress?.Length > 512
            || (physical && (session.PhysicalCapacity == 0 || session.PhysicalBookingDeadline is null))
            || (!physical && (session.PhysicalCapacity != 0 || session.PhysicalBookingDeadline is not null))
            || session.PhysicalBookingDeadline > session.StartsAt)
            throw new CourseIntakeException("INVALID_PHYSICAL_DELIVERY", "Physical delivery requires an address, positive capacity and a booking deadline no later than the session start.", field: "physicalCapacity");
        if (!physical && session.MeetingLink is null)
            throw new CourseIntakeException("DELIVERY_LOCATION_REQUIRED", "Provide an online meeting link or a physical location.", field: "meetingLink");
    }

    public static void Editable(CourseIntake intake)
    {
        if (intake.Status is not (CourseIntakeStatus.Draft or CourseIntakeStatus.Rejected))
            throw new CourseIntakeException("INTAKE_NOT_EDITABLE", "Only Draft or Rejected Intakes can be structurally edited.", 409);
    }

    public static void Version(CourseIntake intake, Guid expected)
    {
        if (expected == Guid.Empty || intake.Version != expected)
            throw new CourseIntakeException("INTAKE_VERSION_CONFLICT", "The Intake has changed. Reload it before trying again.", 409);
    }
}
