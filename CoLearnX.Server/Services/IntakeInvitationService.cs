using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

// The Trainer's side of an Intake invitation: review the fixed schedule, then accept or decline it.
// Accepting also publishes the Intake, so the Creator does not have to come back and do it.
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
        var trainerName = await db.Users.Where(u => u.Id == trainerUserId).Select(u => u.FullName).SingleAsync(ct);
        var subject = $"{intake.Course.Code} {intake.Course.Title} (Intake #{intake.Id})";
        db.AuditLogs.Add(new AuditLog { UserId = trainerUserId, Action = accept ? "CourseIntakeTrainerAccepted" : "CourseIntakeTrainerDeclined",
            EntityType = nameof(CourseIntake), EntityId = intake.Id.ToString(), Result = target.ToString(), Reason = accept ? null : note });

        // Accepting publishes the Intake straight away. When it cannot be published as it stands, the acceptance is
        // still kept and the Creator is told what to fix before publishing it themselves.
        Notification? publishedNotice = null;
        string? publishProblem = null;
        if (accept)
        {
            await JoinCourseRosterAsync(intake, trainerUserId, now, ct);
            try { publishedNotice = await new CourseIntakeService(db, realtime).StagePublishOnAcceptanceAsync(intake, ct); }
            catch (CourseIntakeException problem) { publishProblem = problem.Message; }
        }
        var published = accept && publishProblem is null;
        var notification = new Notification
        {
            UserId = intake.Course.CreatorId, IntakeId = intake.Id,
            Code = accept ? "N-intake-invitation-accepted" : "N-intake-invitation-declined",
            Title = !accept ? "Intake invitation declined"
                : published ? "Trainer accepted, Intake published" : "Trainer accepted, Intake not published yet",
            Body = !accept
                ? $"{trainerName} declined the schedule for {subject}." + (note is null ? "" : $" Reason: {note}")
                    + " Choose another Trainer, or change the schedule and send it again."
                : published
                    ? $"{trainerName} accepted the schedule for {subject}. The Intake was published automatically."
                    : $"{trainerName} accepted the schedule for {subject}, but the Intake could not be published automatically: "
                        + $"{publishProblem} Open the Intake to fix this, then publish it yourself.",
        };
        db.Notifications.Add(notification);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new CourseIntakeException("INTAKE_VERSION_CONFLICT", "The Intake changed. Reload before replying.", 409);
        }
        if (realtime is not null)
        {
            if (publishedNotice is not null) await realtime.PublishAsync(publishedNotice, ct);
            await realtime.PublishAsync(notification, ct);
        }
        return ToDto(intake, await BusySessionsAsync(trainerUserId, ct));
    }

    // Accepting an Intake makes the Trainer one of the course's Trainers, whatever their earlier roster status was.
    private async Task JoinCourseRosterAsync(CourseIntake intake, int trainerUserId, DateTime now, CancellationToken ct)
    {
        var roster = await db.CourseTrainers.SingleOrDefaultAsync(row => row.CourseId == intake.CourseId && row.TrainerId == trainerUserId, ct);
        if (roster is null)
        {
            roster = new CourseTrainer { CourseId = intake.CourseId, TrainerId = trainerUserId, InvitedByUserId = intake.Course.CreatorId,
                InvitedAt = intake.TrainerInvitedAt ?? now };
            db.CourseTrainers.Add(roster);
        }
        roster.Status = CourseTrainerStatus.Accepted;
        roster.RespondedAt = now;
        roster.RemovedAt = null;
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
