using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ITrainerDeliveryService
{
    Task<CourseIntakeDetailDto> UpdateMeetingLinkAsync(int trainerUserId, int courseIntakeId, int courseSessionId,
        UpdateSessionDeliveryRequest request, CancellationToken ct = default);
}

public sealed class TrainerDeliveryService(CoLearnXDbContext db) : ITrainerDeliveryService
{
    public async Task<CourseIntakeDetailDto> UpdateMeetingLinkAsync(int trainerUserId, int courseIntakeId, int courseSessionId,
        UpdateSessionDeliveryRequest request, CancellationToken ct = default)
    {
        if (trainerUserId <= 0)
            throw new CourseIntakeException("UNAUTHENTICATED", "An authenticated User is required.", 401);
        if (!await db.Users.AnyAsync(user => user.Id == trainerUserId && user.IsActive
            && user.Roles.Any(role => role.Role == AppRole.Trainer), ct))
            throw new CourseIntakeException("TRAINER_REQUIRED", "An active Trainer account is required.", 403);
        var intake = await db.CourseIntakes.Include(item => item.Sessions)
            .SingleOrDefaultAsync(item => item.Id == courseIntakeId && item.TrainerId == trainerUserId, ct)
            ?? throw new CourseIntakeException("INTAKE_NOT_FOUND", "Owned Intake was not found.", 404);
        CourseIntakeValidation.Version(intake, request.Version);
        if (intake.Status is not (CourseIntakeStatus.Published or CourseIntakeStatus.InProgress))
            throw new CourseIntakeException("DELIVERY_NOT_EDITABLE", "Delivery links can be changed only for Published or In Progress Intakes.", 409);
        var session = intake.Sessions.SingleOrDefault(item => item.Id == courseSessionId)
            ?? throw new CourseIntakeException("SESSION_NOT_FOUND", "Session was not found in this Intake.", 404);
        session.MeetingLink = NormalizeMeetingLink(request.MeetingLink);
        CourseIntakeValidation.Session(session, intake.StartsAt, intake.EndsAt);
        intake.Version = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            UserId = trainerUserId,
            Action = "CourseSessionDeliveryUpdated",
            EntityType = nameof(CourseIntake),
            EntityId = intake.Id.ToString(),
            Result = intake.Status.ToString(),
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new CourseIntakeException("INTAKE_VERSION_CONFLICT", "The Intake changed concurrently. Reload before retrying.", 409);
        }
        return ToDto(intake);
    }

    private static string? NormalizeMeetingLink(string? link)
    {
        var normalized = string.IsNullOrWhiteSpace(link) ? null : link.Trim();
        if (normalized is not null && (normalized.Length > 2048
            || !Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
            throw new CourseIntakeException("INVALID_MEETING_LINK", "Use an absolute HTTP or HTTPS meeting link.", field: "meetingLink");
        return normalized;
    }

    private static CourseIntakeDetailDto ToDto(CourseIntake intake)
        => new(intake.Id, intake.CourseId, intake.TrainerId, intake.RegistrationOpensAt, intake.RegistrationClosesAt,
            intake.StartsAt, intake.EndsAt, intake.Status.ToString(), intake.SubmittedAt, intake.ConfirmedByCreatorId,
            intake.ConfirmedAt, intake.ConfirmationNote, intake.Version,
            intake.Sessions.OrderBy(session => session.StartsAt).ThenBy(session => session.Id)
                .Select(session => new CourseSessionDto(session.Id, session.Label, session.StartsAt, session.EndsAt,
                    session.PhysicalCapacity, session.SeatsLeft, session.CourseIntakeId, session.MeetingLink,
                    session.PhysicalAddress, session.PhysicalCapacity, session.PhysicalBookingDeadline)).ToList());
}
