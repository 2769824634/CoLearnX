using System.Data;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public record IntakeSettlementResult(int IntakeId, bool Confirmed, int ProcessedCount);

public interface IIntakeSettlementService
{
    Task<IntakeSettlementResult> SettleIfDueAsync(int intakeId, CancellationToken ct = default);
    Task<IntakeSettlementResult> CancelIntakeAsync(int trainerUserId, int intakeId, CancellationToken ct = default);
    Task ProcessDueAsync(CancellationToken ct = default);
}

public sealed class IntakeSettlementService(CoLearnXDbContext db) : IIntakeSettlementService
{
    public async Task<IntakeSettlementResult> CancelIntakeAsync(int trainerUserId, int intakeId, CancellationToken ct = default)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var intake = await db.CourseIntakes.AsNoTracking().SingleOrDefaultAsync(i => i.Id == intakeId && i.TrainerId == trainerUserId, ct)
                ?? throw new CourseException("INTAKE_NOT_FOUND", "Owned Intake was not found.", 404);
            if (intake.CancelledAt is not null && intake.Status == CourseIntakeStatus.Cancelled)
                return new IntakeSettlementResult(intakeId, false, 0);
            if (intake.Status is not (CourseIntakeStatus.Published or CourseIntakeStatus.InProgress))
                throw new CourseException("INTAKE_NOT_CANCELLABLE", "Only a published class can be cancelled.", 409);
            var now = DateTime.UtcNow;
            var changed = await db.CourseIntakes.Where(i => i.Id == intakeId && i.CancelledAt == null
                    && (i.Status == CourseIntakeStatus.Published || i.Status == CourseIntakeStatus.InProgress))
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.CancelledAt, now)
                    .SetProperty(i => i.CancellationReason, "TrainerCancelled")
                    .SetProperty(i => i.Version, Guid.NewGuid())
                    .SetProperty(i => i.Status, CourseIntakeStatus.Cancelled), ct);
            if (changed != 1)
                return new IntakeSettlementResult(intakeId, false, 0);
            var enrollments = await db.Enrollments.Include(e => e.User).Include(e => e.Course)
                .Include(e => e.CourseSession)
                .Where(e => e.CourseSession.CourseIntakeId == intakeId
                    && (e.Status == EnrollmentStatus.Reserved || e.Status == EnrollmentStatus.Active))
                .ToListAsync(ct);
            var credits = new EnrollmentCreditTransitions(db);
            foreach (var enrollment in enrollments)
            {
                var reserved = enrollment.Status == EnrollmentStatus.Reserved;
                var description = $"Class cancelled: {enrollment.Course.Code}";
                if (reserved)
                    credits.Release(enrollment.User, enrollment, description, inconsistentHoldIsDataError: true);
                else
                    credits.Refund(enrollment.User, enrollment, enrollment.CreditsSpent, description);
                db.Notifications.Add(new Notification { UserId = enrollment.UserId, IntakeId = intake.Id, EnrollmentId = enrollment.Id, EmailPending = true,
                    Code = reserved ? "N-hold-released" : "N-class-cancelled",
                    Title = "Class cancelled", Body = $"{BusinessText.Credits(enrollment.CreditsSpent)} {(enrollment.CreditsSpent == 1 ? "was" : "were")} returned for {enrollment.Course.Code}." });
            }
            var course = await db.Courses.AsNoTracking().SingleAsync(c => c.Id == intake.CourseId, ct);
            db.Notifications.Add(new Notification { UserId = intake.TrainerId, IntakeId = intake.Id, EmailPending = true, Code = "N-intake-cancelled",
                Title = "Class cancelled", Body = $"{course.Code} Intake #{intakeId} was cancelled." });
            db.Notifications.Add(new Notification { UserId = course.CreatorId, IntakeId = intake.Id, EmailPending = true, Code = "N-intake-cancelled-creator",
                Title = "Class cancelled", Body = $"{course.Code} Intake #{intakeId} was cancelled." });
            db.AuditLogs.Add(new AuditLog { UserId = trainerUserId, Action = "CourseIntakeCancelled",
                EntityType = nameof(CourseIntake), EntityId = intakeId.ToString(), Result = "Succeeded" });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new IntakeSettlementResult(intakeId, false, enrollments.Count);
        });
    }

    public async Task ProcessDueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await SendScheduledNotificationsAsync(now, ct);
        var ids = await db.CourseIntakes.AsNoTracking()
            .Where(i => i.Status == CourseIntakeStatus.Published && i.RegistrationClosesAt <= now
                && i.ConfirmedToRunAt == null && i.CancelledAt == null
                && (!i.Sessions.SelectMany(s => s.Enrollments).Any(e => e.Status == EnrollmentStatus.Active)
                    || i.Sessions.SelectMany(s => s.Enrollments).Any(e => e.Status == EnrollmentStatus.Reserved)))
            .Select(i => i.Id).ToListAsync(ct);
        foreach (var id in ids)
            await SettleIfDueAsync(id, ct);
        await db.CourseIntakes.Where(i => i.Status == CourseIntakeStatus.Published && i.ConfirmedToRunAt != null
            && i.CancelledAt == null && i.StartsAt <= now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.Status, CourseIntakeStatus.InProgress)
                .SetProperty(i => i.Version, Guid.NewGuid()), ct);
    }

    private async Task SendScheduledNotificationsAsync(DateTime now, CancellationToken ct)
    {
        var approaching = await db.CourseIntakes.AsNoTracking().Include(i => i.Course)
            .Where(i => i.Status == CourseIntakeStatus.Published && i.CancelledAt == null
                && i.ConfirmedToRunAt == null && i.RegistrationClosesAt > now
                && i.RegistrationClosesAt <= now.AddDays(3))
            .ToListAsync(ct);
        foreach (var intake in approaching)
        {
            var count = await db.Enrollments.CountAsync(e => e.CourseSession.CourseIntakeId == intake.Id
                && (e.Status == EnrollmentStatus.Reserved || e.Status == EnrollmentStatus.Active), ct);
            if (count >= intake.MinEnrollment) continue;
            var body = $"{intake.Course.Code} Intake #{intake.Id} is below its {intake.MinEnrollment}-learner minimum with three days or fewer before registration closes.";
            if (!await db.Notifications.AnyAsync(n => n.UserId == intake.TrainerId
                && n.Code == "N-under-enrolled" && n.Body == body, ct))
                db.Notifications.Add(new Notification { UserId = intake.TrainerId, IntakeId = intake.Id, EmailPending = true, Code = "N-under-enrolled",
                    Title = "Minimum enrollment at risk", Body = body });
        }

        var reminders = await db.Enrollments.AsNoTracking().Include(e => e.Course)
            .Include(e => e.CourseSession).ThenInclude(s => s.CourseIntake)
            .Where(e => e.Status == EnrollmentStatus.Active && e.CourseSession.CourseIntake.ConfirmedToRunAt != null
                && e.CourseSession.CourseIntake.StartsAt > now
                && e.CourseSession.CourseIntake.StartsAt <= now.AddDays(7))
            .ToListAsync(ct);
        foreach (var enrollment in reminders)
        {
            var intake = enrollment.CourseSession.CourseIntake;
            var session = enrollment.CourseSession;
            var place = session.PhysicalCapacity > 0 ? session.PhysicalAddress : session.MeetingLink;
            var body = $"{enrollment.Course.Code} Intake #{intake.Id} starts {session.StartsAt:u}."
                + (string.IsNullOrWhiteSpace(place) ? " Check your program for details." : $" Location: {place}");
            if (!await db.Notifications.AnyAsync(n => n.UserId == enrollment.UserId
                && n.Code == "N-class-reminder" && n.Body == body, ct))
                db.Notifications.Add(new Notification { UserId = enrollment.UserId, IntakeId = intake.Id, EnrollmentId = enrollment.Id, EmailPending = true, Code = "N-class-reminder",
                    Title = "Class starts soon", Body = body });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IntakeSettlementResult> SettleIfDueAsync(int intakeId, CancellationToken ct = default)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var now = DateTime.UtcNow;
            var intake = await db.CourseIntakes.AsNoTracking().SingleOrDefaultAsync(i => i.Id == intakeId, ct)
                ?? throw new CourseException("INTAKE_NOT_FOUND", "Intake was not found.", 404);
            if (intake.Status != CourseIntakeStatus.Published || intake.RegistrationClosesAt > now
                || intake.ConfirmedToRunAt is not null || intake.CancelledAt is not null)
                return new IntakeSettlementResult(intakeId, intake.ConfirmedToRunAt is not null, 0);

            var enrollments = await db.Enrollments.Include(e => e.User).Include(e => e.Course)
                .Include(e => e.CourseSession)
                .Where(e => e.CourseSession.CourseIntakeId == intakeId
                    && (e.Status == EnrollmentStatus.Reserved || e.Status == EnrollmentStatus.Active))
                .ToListAsync(ct);
            var reservations = enrollments.Where(e => e.Status == EnrollmentStatus.Reserved).ToList();
            if (reservations.Count == 0 && enrollments.Count > 0)
                return new IntakeSettlementResult(intakeId, false, 0); // Historical Active enrollments were already charged.
            var confirmed = enrollments.Count >= intake.MinEnrollment;
            var unsettled = db.CourseIntakes.Where(i => i.Id == intakeId && i.Status == CourseIntakeStatus.Published
                && i.ConfirmedToRunAt == null && i.CancelledAt == null);
            var changed = confirmed
                ? await unsettled.ExecuteUpdateAsync(setters => setters.SetProperty(i => i.ConfirmedToRunAt, now)
                    .SetProperty(i => i.Version, Guid.NewGuid()), ct)
                : await unsettled.ExecuteUpdateAsync(setters => setters.SetProperty(i => i.CancelledAt, now)
                    .SetProperty(i => i.CancellationReason, "MinimumEnrollmentNotMet")
                    .SetProperty(i => i.Version, Guid.NewGuid())
                    .SetProperty(i => i.Status, CourseIntakeStatus.Cancelled), ct);
            if (changed != 1)
                return new IntakeSettlementResult(intakeId, false, 0);

            var credits = new EnrollmentCreditTransitions(db);
            foreach (var enrollment in reservations)
            {
                var user = enrollment.User;
                if (confirmed)
                    credits.Capture(user, enrollment, $"Class confirmed: {enrollment.Course.Code}");
                else
                {
                    credits.Release(user, enrollment, $"Class did not run: {enrollment.Course.Code}",
                        inconsistentHoldIsDataError: true);
                    enrollment.PostponementEligible = true;
                }
                db.Notifications.Add(new Notification { UserId = user.Id, IntakeId = intake.Id, EnrollmentId = enrollment.Id, EmailPending = true,
                    Code = confirmed ? "N-class-confirmed" : "N-hold-released",
                    Title = confirmed ? "Class confirmed" : "Credits released",
                    Body = confirmed ? $"Your place in {enrollment.Course.Code} is confirmed."
                        : $"{BusinessText.Credits(enrollment.CreditsSpent)} {(enrollment.CreditsSpent == 1 ? "is" : "are")} available again because {enrollment.Course.Code} Intake #{intake.Id} did not meet the minimum enrollment. "
                            + $"You can wait up to 7 days (until {now.AddDays(7):u}) for a postponed class. "
                            + "If one is published, open My Programs to choose a session and reserve again using your available credits. A replacement is not guaranteed; your credits remain available until you reserve again." });
            }
            var course = await db.Courses.AsNoTracking().SingleAsync(c => c.Id == intake.CourseId, ct);
            var code = confirmed ? "N-intake-confirmed" : "N-intake-cancelled";
            var title = confirmed ? "Class confirmed" : "Class cancelled";
            var body = $"{course.Code} Intake #{intakeId} {(confirmed ? "will run" : "did not meet minimum enrollment")}.";
            db.Notifications.Add(new Notification { UserId = intake.TrainerId, IntakeId = intake.Id, EmailPending = true, Code = code, Title = title, Body = body });
            db.Notifications.Add(new Notification { UserId = course.CreatorId, IntakeId = intake.Id, EmailPending = true, Code = $"{code}-creator", Title = title, Body = body });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new IntakeSettlementResult(intakeId, confirmed, reservations.Count);
        });
    }
}

public sealed class IntakeSettlementWorker(IServiceScopeFactory scopes, ILogger<IntakeSettlementWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Intake settlement scan failed"); }
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<NotificationEmailDispatcher>().DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Business email dispatch scan failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
