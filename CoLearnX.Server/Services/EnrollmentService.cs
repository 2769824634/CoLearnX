using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IEnrollmentService
{
    Task<EnrolResultDto> EnrolAsync(int userId, EnrolRequest request, CancellationToken ct = default);
    Task<EnrolResultDto> AcceptPostponementAsync(int userId, int enrollmentId, AcceptPostponementRequest request, CancellationToken ct = default);
    Task<EnrollmentDto> CancelReservationAsync(int userId, int enrollmentId, CancellationToken ct = default);
    Task<EnrollmentDto> WithdrawAsync(int userId, int enrollmentId, CancellationToken ct = default);
    Task<IReadOnlyList<EnrollmentDto>> GetMyAsync(int userId, CancellationToken ct = default);
}

public class EnrollmentService(CoLearnXDbContext db) : IEnrollmentService
{
    public async Task<EnrolResultDto> EnrolAsync(int userId, EnrolRequest request, CancellationToken ct = default)
        => await ReserveAsync(userId, request, null, ct);

    public async Task<EnrolResultDto> AcceptPostponementAsync(int userId, int enrollmentId,
        AcceptPostponementRequest request, CancellationToken ct = default)
        => await ReserveAsync(userId, new EnrolRequest(0, request.CourseSessionId), enrollmentId, ct);

    private async Task<EnrolResultDto> ReserveAsync(int userId, EnrolRequest request, int? sourceEnrollmentId, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var now = DateTime.UtcNow;
            Enrollment? original = null;
            if (sourceEnrollmentId is not null)
            {
                original = await db.Enrollments.AsNoTracking().Include(e => e.CourseSession).ThenInclude(s => s.CourseIntake)
                    .SingleOrDefaultAsync(e => e.Id == sourceEnrollmentId && e.UserId == userId, ct)
                    ?? throw new CourseException("ENROLLMENT_NOT_FOUND", "Enrollment was not found.", 404);
                var source = original.CourseSession.CourseIntake;
                if (!original.PostponementEligible || original.Status != EnrollmentStatus.Cancelled
                    || source.CancellationReason != "MinimumEnrollmentNotMet" || source.CancelledAt is null
                    || now > source.CancelledAt.Value.AddDays(7))
                    throw new CourseException("POSTPONEMENT_NOT_AVAILABLE", "This reservation has no available postponement invitation.", 409);
                if (await db.Enrollments.AnyAsync(e => e.PostponedFromEnrollmentId == original.Id, ct))
                    throw new CourseException("POSTPONEMENT_ALREADY_ACCEPTED", "This postponement invitation was already accepted.", 409);
                request = request with { CourseId = original.CourseId };
            }
            var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.CourseId && c.Status == CourseStatus.Published, ct)
                ?? throw new CourseException("COURSE_NOT_AVAILABLE", "Course is not available.", 404);
            var session = await db.CourseSessions.AsNoTracking().Include(s => s.CourseIntake)
                .SingleOrDefaultAsync(s => s.Id == request.CourseSessionId && s.CourseIntake.CourseId == course.Id, ct)
                ?? throw new CourseException("SESSION_NOT_FOUND", "Session was not found.", 404);
            var intake = session.CourseIntake;
            if (original is not null && (intake.ReplacementForIntakeId != original.CourseSession.CourseIntakeId
                || intake.Status != CourseIntakeStatus.Published || intake.ConfirmedToRunAt is not null))
                throw new CourseException("POSTPONEMENT_NOT_AVAILABLE", "Choose a published replacement session for the original class.", 409);
            if (intake.Status != CourseIntakeStatus.Published || intake.CancelledAt is not null
                || intake.ConfirmedToRunAt is not null || now >= intake.RegistrationClosesAt)
                throw new CourseException("REGISTRATION_CLOSED", "Registration is closed.");
            if (now < intake.RegistrationOpensAt)
                throw new CourseException("REGISTRATION_NOT_OPEN", "Registration is not open yet.");
            if (session.PhysicalCapacity > 0 && session.PhysicalBookingDeadline < now)
                throw new CourseException("PHYSICAL_BOOKING_CLOSED", "Physical booking is closed.");
            if (await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == course.Id
                    && (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Reserved), ct))
                throw new CourseException("ALREADY_ENROLLED", "You already have a place in this course.", 409);
            var classSize = await db.Enrollments.CountAsync(e => e.CourseSession.CourseIntakeId == intake.Id
                && (e.Status == EnrollmentStatus.Active || e.Status == EnrollmentStatus.Reserved), ct);

            var cost = original?.CreditsSpent ?? course.CreditCost;

            // Claim the aggregate version so a concurrent structural change cannot strand a new hold.
            var claimed = await db.CourseIntakes.Where(i => i.Id == intake.Id && i.Version == intake.Version
                && i.Status == CourseIntakeStatus.Published && i.ConfirmedToRunAt == null && i.CancelledAt == null
                && i.RegistrationClosesAt > now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.Version, Guid.NewGuid()), ct);
            if (claimed != 1)
                throw new CourseException("INTAKE_VERSION_CONFLICT", "The class changed. Refresh before reserving again.", 409);

            var credits = new EnrollmentCreditTransitions(db);
            var wallet = await credits.HoldCreditsAndSeatAsync(userId, session.Id, cost, ct);
            var enrollment = new Enrollment { UserId = userId, CourseId = course.Id, CourseSessionId = session.Id,
                Status = EnrollmentStatus.Reserved, ProgressPercent = 0, CreditsSpent = cost,
                PostponedFromEnrollmentId = original?.Id };
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync(ct);
            credits.RecordHold(userId, enrollment.Id, cost, wallet.BalanceAfter, wallet.HeldAfter,
                $"Reserved {course.Code} — {course.Title}");
            db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "EnrollmentReserved",
                EntityType = nameof(Enrollment), EntityId = enrollment.Id.ToString(), Result = "Succeeded",
                Reason = $"Held {BusinessText.Credits(cost)} for {course.Code}" });
            db.Notifications.Add(new Notification { UserId = userId, IntakeId = intake.Id, EnrollmentId = enrollment.Id, Code = "N-01", Title = "Place reserved",
                Body = $"{BusinessText.Credits(cost)} {(cost == 1 ? "is" : "are")} on hold for {course.Code}." });
            if (session.PhysicalCapacity > 0 && session.SeatsTaken + 1 == session.PhysicalCapacity)
                db.Notifications.Add(new Notification { UserId = intake.TrainerId, IntakeId = intake.Id, Code = "N-session-full",
                    Title = "Session is full", Body = $"{course.Code} Intake #{intake.Id} has reached physical capacity." });
            if (classSize + 1 == intake.MinEnrollment)
            {
                var body = $"{course.Code} Intake #{intake.Id} has enough learners to run.";
                if (!await db.Notifications.AnyAsync(n => n.UserId == intake.TrainerId
                    && n.Code == "N-min-reached" && n.Body == body, ct))
                    db.Notifications.Add(new Notification { UserId = intake.TrainerId, IntakeId = intake.Id, Code = "N-min-reached",
                        Title = "Minimum enrollment reached", Body = body });
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new EnrolResultDto(enrollment.Id, cost, wallet.BalanceAfter,
                course.Code, course.Title, wallet.HeldAfter);
        });
    }

    public async Task<EnrollmentDto> CancelReservationAsync(int userId, int enrollmentId, CancellationToken ct = default)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var enrollment = await db.Enrollments.Include(e => e.Course).ThenInclude(c => c.Trainer)
                .Include(e => e.CourseSession).ThenInclude(s => s.CourseIntake).ThenInclude(i => i.Trainer)
                .SingleOrDefaultAsync(e => e.Id == enrollmentId && e.UserId == userId, ct)
                ?? throw new CourseException("ENROLLMENT_NOT_FOUND", "Enrollment was not found.", 404);
            if (enrollment.Status != EnrollmentStatus.Reserved || enrollment.CourseSession.CourseIntake.ConfirmedToRunAt is not null)
                throw new CourseException("RESERVATION_NOT_CANCELLABLE", "Only an unconfirmed reservation can be cancelled.", 409);
            var wallet = await db.Users.SingleAsync(u => u.Id == userId, ct);
            new EnrollmentCreditTransitions(db).Release(wallet, enrollment,
                $"Released {enrollment.Course.Code} reservation");
            db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "ReservationCancelled",
                EntityType = nameof(Enrollment), EntityId = enrollment.Id.ToString(), Result = "Succeeded",
                Reason = $"Released {BusinessText.Credits(enrollment.CreditsSpent)}" });
            db.Notifications.Add(new Notification { UserId = userId, IntakeId = enrollment.CourseSession.CourseIntakeId, EnrollmentId = enrollment.Id, Code = "N-hold-released", Title = "Credits released",
                Body = $"{BusinessText.Credits(enrollment.CreditsSpent)} {(enrollment.CreditsSpent == 1 ? "is" : "are")} available again." });
            var intake = enrollment.CourseSession.CourseIntake;
            if (enrollment.CourseSession.PhysicalCapacity > 0
                && enrollment.CourseSession.SeatsTaken + 1 == enrollment.CourseSession.PhysicalCapacity
                && DateTime.UtcNow < intake.RegistrationClosesAt)
                db.Notifications.Add(new Notification { UserId = intake.TrainerId, IntakeId = intake.Id, Code = "N-session-reopened",
                    Title = "Session has a place available", Body = $"{enrollment.Course.Code} Intake #{intake.Id} has a free physical place." });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToEnrollmentDto(enrollment);
        });
    }

    public async Task<EnrollmentDto> WithdrawAsync(int userId, int enrollmentId, CancellationToken ct = default)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var enrollment = await db.Enrollments.Include(e => e.Course).ThenInclude(c => c.Trainer)
                .Include(e => e.CourseSession).ThenInclude(s => s.CourseIntake).ThenInclude(i => i.Trainer)
                .SingleOrDefaultAsync(e => e.Id == enrollmentId && e.UserId == userId, ct)
                ?? throw new CourseException("ENROLLMENT_NOT_FOUND", "Enrollment was not found.", 404);
            if (enrollment.Status != EnrollmentStatus.Active)
                throw new CourseException("WITHDRAW_NOT_ALLOWED", "Only an active enrollment can be withdrawn.", 409);
            var daysLeft = (enrollment.CourseSession.CourseIntake.StartsAt.Date - DateTime.UtcNow.Date).Days;
            if (daysLeft <= 5)
                throw new CourseException("WITHDRAW_TOO_LATE", "Withdrawal closes five calendar days before the class starts.", 409);
            if (daysLeft > 10)
                throw new CourseException("WITHDRAW_NOT_OPEN", "Confirmed enrollment withdrawal opens ten calendar days before the class starts.", 409);
            var refund = (int)Math.Round(enrollment.CreditsSpent * 0.7m, MidpointRounding.AwayFromZero);
            var forfeited = enrollment.CreditsSpent - refund;
            var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
            new EnrollmentCreditTransitions(db).Refund(user, enrollment, refund,
                $"Withdrawal refund: {enrollment.Course.Code}",
                forfeited > 0 ? $"{forfeited} credits retained after withdrawal from {enrollment.Course.Code}" : null);
            db.AuditLogs.Add(new AuditLog { UserId = userId, Action = "EnrollmentWithdrawn",
                EntityType = nameof(Enrollment), EntityId = enrollment.Id.ToString(), Result = "Succeeded",
                Reason = $"Refunded {refund}; forfeited {forfeited} credits" });
            db.Notifications.Add(new Notification { UserId = userId, IntakeId = enrollment.CourseSession.CourseIntakeId, EnrollmentId = enrollment.Id, Code = "N-withdraw-70", Title = "Enrollment withdrawn",
                Body = $"{refund} credits were refunded for {enrollment.Course.Code}." });
            db.Notifications.Add(new Notification { UserId = enrollment.CourseSession.CourseIntake.TrainerId,
                IntakeId = enrollment.CourseSession.CourseIntakeId,
                Code = "N-learner-withdrew", Title = "Learner withdrew",
                Body = $"A learner withdrew from {enrollment.Course.Code}." });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToEnrollmentDto(enrollment);
        });
    }

    public async Task<IReadOnlyList<EnrollmentDto>> GetMyAsync(int userId, CancellationToken ct = default)
    {
        var enrollments = await db.Enrollments.AsNoTracking()
            .Include(e => e.Course).ThenInclude(c => c.Trainer)
            .Include(e => e.CourseSession).ThenInclude(s => s.CourseIntake).ThenInclude(i => i.Trainer)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        var eligible = enrollments.Where(e => e.PostponementEligible && e.Status == EnrollmentStatus.Cancelled
            && e.CourseSession.CourseIntake.CancelledAt?.AddDays(7) >= now
            && !enrollments.Any(next => next.PostponedFromEnrollmentId == e.Id)
            && !enrollments.Any(active => active.CourseId == e.CourseId
                && active.Status is EnrollmentStatus.Active or EnrollmentStatus.Reserved)).ToList();
        var sourceIds = eligible.Select(e => e.CourseSession.CourseIntakeId).ToList();
        var replacements = await db.CourseSessions.AsNoTracking().Include(s => s.CourseIntake)
            .Where(s => s.CourseIntake.ReplacementForIntakeId != null
                && sourceIds.Contains(s.CourseIntake.ReplacementForIntakeId.Value)
                && s.CourseIntake.Status == CourseIntakeStatus.Published && s.CourseIntake.Course.Status == CourseStatus.Published
                && s.CourseIntake.CancelledAt == null && s.CourseIntake.ConfirmedToRunAt == null
                && s.CourseIntake.RegistrationOpensAt <= now && s.CourseIntake.RegistrationClosesAt > now
                && (s.PhysicalCapacity == 0 || (s.SeatsTaken < s.PhysicalCapacity && s.PhysicalBookingDeadline >= now)))
            .OrderBy(s => s.StartsAt).ToListAsync(ct);
        return enrollments.Select(e => ToEnrollmentDto(e) with
        {
            PostponementOptions = eligible.Contains(e) ? replacements
                .Where(s => s.CourseIntake.ReplacementForIntakeId == e.CourseSession.CourseIntakeId)
                .Select(s => new PostponementOptionDto(s.CourseIntakeId, s.Id, s.Label, s.StartsAt, s.EndsAt,
                    s.CourseIntake.RegistrationClosesAt, e.CreditsSpent,
                    s.PhysicalCapacity == 0 ? null : s.PhysicalCapacity - s.SeatsTaken)).ToList() : [],
        }).ToList();
    }

    private static EnrollmentDto ToEnrollmentDto(Enrollment e)
        => new(e.Id, e.CourseId, e.Course.Code, e.Course.Title, e.CourseSession.CourseIntake.Trainer.FullName,
            e.Status.ToString(), e.ProgressPercent, e.CourseSessionId,
            e.Status == EnrollmentStatus.Reserved ? null : e.CourseSession.MeetingLink,
            e.Status == EnrollmentStatus.Reserved ? e.CreditsSpent : 0,
            e.CourseSession.CourseIntake.RegistrationClosesAt,
            e.CourseSession.CourseIntake.StartsAt, [], WithdrawalRefund(e));

    private static int? WithdrawalRefund(Enrollment e)
    {
        var days = (e.CourseSession.CourseIntake.StartsAt.Date - DateTime.UtcNow.Date).Days;
        return e.Status == EnrollmentStatus.Active && days is > 5 and <= 10
            ? (int)Math.Round(e.CreditsSpent * 0.7m, MidpointRounding.AwayFromZero) : null;
    }
}
