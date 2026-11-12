using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

// The Trainer's side of an Intake invitation: review the fixed schedule, then accept or decline it.
public interface IIntakeInvitationService
{
    Task<IReadOnlyList<IntakeInvitationDto>> ListForTrainerAsync(int trainerUserId, CancellationToken ct = default);
    Task<IntakeInvitationDto> RespondAsync(int trainerUserId, int courseIntakeId, bool accept, string? reason, CancellationToken ct = default);
}

public sealed class IntakeInvitationService(CoLearnXDbContext db, INotificationRealtime? realtime = null) : IIntakeInvitationService
{
    private const int MaxReasonLength = 500;

    public async Task<IReadOnlyList<IntakeInvitationDto>> ListForTrainerAsync(int trainerUserId, CancellationToken ct = default)
    {
        await RequireTrainerAsync(trainerUserId, ct);
        var intakes = await InvitationsFor(trainerUserId).AsNoTracking().ToListAsync(ct);
        var busy = await BusySessionsAsync(trainerUserId, ct);
        return intakes
            .OrderBy(i => i.TrainerInvitation == TrainerInvitationStatus.Invited ? 0 : 1)
            .ThenByDescending(i => i.TrainerInvitedAt ?? i.TrainerRespondedAt ?? DateTime.MinValue)
            .ThenByDescending(i => i.Id)
            .Select(i => ToDto(i, busy)).ToList();
    }

    public async Task<IntakeInvitationDto> RespondAsync(int trainerUserId, int courseIntakeId, bool accept, string? reason,
        CancellationToken ct = default)
    {
        await RequireTrainerAsync(trainerUserId, ct);
        var intake = await InvitationsFor(trainerUserId).SingleOrDefaultAsync(i => i.Id == courseIntakeId, ct)
            ?? throw new CourseIntakeException("INVITATION_NOT_FOUND", "This invitation was not found.", 404);
        var target = accept ? TrainerInvitationStatus.Accepted : TrainerInvitationStatus.Declined;
        if (intake.TrainerInvitation == target)
            return ToDto(intake, await BusySessionsAsync(trainerUserId, ct));
        if (intake.TrainerInvitation != TrainerInvitationStatus.Invited
            || intake.Status is not (CourseIntakeStatus.Draft or CourseIntakeStatus.Rejected))
            throw new CourseIntakeException("INVITATION_NOT_PENDING", "This invitation is no longer waiting for a reply.", 409);
        var note = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (note?.Length > MaxReasonLength)
            throw new CourseIntakeException("INVALID_REASON", $"Keep the reason under {MaxReasonLength} characters.", field: "reason");

        var now = DateTime.UtcNow;
        intake.TrainerInvitation = target;
        intake.TrainerRespondedAt = now;
        intake.TrainerDeclineReason = accept ? null : note;
        intake.Version = Guid.NewGuid();
        if (accept)
        {
            // A Trainer who only applied to the course joins it by agreeing to teach this Intake.
            var applied = await db.CourseTrainers.SingleOrDefaultAsync(row => row.CourseId == intake.CourseId
                && row.TrainerId == trainerUserId && row.Status == CourseTrainerStatus.Requested, ct);
            if (applied is not null)
            {
                applied.Status = CourseTrainerStatus.Accepted;
                applied.RespondedAt = now;
            }
        }

        var trainerName = await db.Users.Where(u => u.Id == trainerUserId).Select(u => u.FullName).SingleAsync(ct);
        var subject = $"{intake.Course.Code} {intake.Course.Title} (Intake #{intake.Id})";
        var notification = new Notification
        {
            UserId = intake.Course.CreatorId, IntakeId = intake.Id,
            Code = accept ? "N-intake-invitation-accepted" : "N-intake-invitation-declined",
            Title = accept ? "Intake invitation accepted" : "Intake invitation declined",
            Body = accept
                ? $"{trainerName} accepted the schedule for {subject}. You can publish it now."
                : $"{trainerName} declined the schedule for {subject}." + (note is null ? "" : $" Reason: {note}")
                    + " Choose another Trainer, or change the schedule and send it again.",
        };
        db.Notifications.Add(notification);
        db.AuditLogs.Add(new AuditLog { UserId = trainerUserId, Action = accept ? "CourseIntakeTrainerAccepted" : "CourseIntakeTrainerDeclined",
            EntityType = nameof(CourseIntake), EntityId = intake.Id.ToString(), Result = target.ToString(), Reason = accept ? null : note });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new CourseIntakeException("INTAKE_VERSION_CONFLICT", "The Intake changed. Reload before replying.", 409);
        }
        if (realtime is not null) await realtime.PublishAsync(notification, ct);
        return ToDto(intake, await BusySessionsAsync(trainerUserId, ct));
    }

    private IQueryable<CourseIntake> InvitationsFor(int trainerUserId)
        => db.CourseIntakes.Include(i => i.Course).ThenInclude(c => c.Creator).Include(i => i.Sessions).AsSplitQuery()
            .Where(i => i.TrainerId == trainerUserId && i.TrainerInvitation != TrainerInvitationStatus.NotSent
                && i.Status != CourseIntakeStatus.Cancelled && i.Status != CourseIntakeStatus.Completed);

    // Sessions the Trainer has already committed to teach: published or running Intakes, and schedules they accepted.
    private async Task<List<BusySession>> BusySessionsAsync(int trainerUserId, CancellationToken ct)
        => await db.CourseSessions.AsNoTracking()
            .Where(s => s.CourseIntake.TrainerId == trainerUserId
                && s.CourseIntake.Status != CourseIntakeStatus.Cancelled && s.CourseIntake.Status != CourseIntakeStatus.Completed
                && (s.CourseIntake.Status == CourseIntakeStatus.Published || s.CourseIntake.Status == CourseIntakeStatus.InProgress
                    || s.CourseIntake.TrainerInvitation == TrainerInvitationStatus.Accepted))
            .Select(s => new BusySession(s.CourseIntakeId, s.CourseIntake.Course.Code, s.Label, s.StartsAt, s.EndsAt))
            .ToListAsync(ct);

    private static IntakeInvitationDto ToDto(CourseIntake i, IReadOnlyList<BusySession> busy)
        => new(i.Id, i.CourseId, i.Course.Code, i.Course.Title, i.Course.Creator?.FullName ?? "", i.TrainerInvitation.ToString(),
            i.Status.ToString(), i.TrainerInvitedAt, i.TrainerRespondedAt, i.TrainerDeclineReason, i.RegistrationClosesAt,
            i.StartsAt, i.EndsAt, i.MinEnrollment,
            i.Sessions.OrderBy(s => s.StartsAt).ThenBy(s => s.Id).Select(s => new IntakeInvitationSessionDto(s.Id, s.Label,
                s.StartsAt, s.EndsAt, s.MeetingLink, s.PhysicalAddress,
                busy.Where(b => b.IntakeId != i.Id && b.StartsAt < s.EndsAt && s.StartsAt < b.EndsAt)
                    .OrderBy(b => b.StartsAt)
                    .Select(b => new ScheduleClashDto(b.IntakeId, b.CourseCode, b.Label, b.StartsAt, b.EndsAt)).ToList()))
                .ToList());

    private async Task RequireTrainerAsync(int userId, CancellationToken ct)
    {
        if (userId <= 0) throw new CourseIntakeException("UNAUTHENTICATED", "An authenticated User is required.", 401);
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Roles.Any(r => r.Role == AppRole.Trainer), ct))
            throw new CourseIntakeException("TRAINER_REQUIRED", "An active Trainer account is required.", 403);
    }

    private sealed record BusySession(int IntakeId, string CourseCode, string Label, DateTime StartsAt, DateTime EndsAt);
}
