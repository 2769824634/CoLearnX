using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CoLearnX.Server.Services;

// B2 must supply the authenticated User ID, never an AdminAccount ID or a body-supplied owner.
public interface ICourseIntakeService
{
    Task<IReadOnlyList<CourseIntakeSummaryDto>> ListOwnedAsync(int trainerUserId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> GetOwnedAsync(int trainerUserId, int courseIntakeId, CancellationToken ct = default);
    Task<IReadOnlyList<CourseIntakeSummaryDto>> ListForCreatorAsync(int creatorUserId, int? courseId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> GetForCreatorAsync(int creatorUserId, int courseIntakeId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> CreateAsync(int creatorUserId, int courseId, CreateCourseIntakeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AdminIntakeDatesDto>> ListForAdminAsync(int adminAccountId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> GetForAdminAsync(int adminAccountId, int courseIntakeId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> UpdateTeachingDatesForAdminAsync(int adminAccountId, int courseIntakeId, UpdateIntakeTeachingDatesRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> PostponeAsync(int creatorUserId, int intakeId, CreateCourseIntakeRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> UpdateAsync(int creatorUserId, int courseIntakeId, UpdateCourseIntakeRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> AddSessionAsync(int creatorUserId, int courseIntakeId, CreateCourseSessionRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> UpdateSessionAsync(int creatorUserId, int courseIntakeId, int courseSessionId, UpdateCourseSessionRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> DeleteSessionAsync(int creatorUserId, int courseIntakeId, int courseSessionId, Guid version, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> PublishAsync(int creatorUserId, int courseIntakeId, Guid version, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> OpenClassAsync(int creatorUserId, int courseId, OpenClassRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> SendToTrainerAsync(int creatorUserId, int courseIntakeId, Guid version, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> WithdrawTrainerInvitationAsync(int creatorUserId, int courseIntakeId, Guid version, CancellationToken ct = default);
}

public sealed class CourseIntakeService(CoLearnXDbContext db, INotificationRealtime? realtime = null) : ICourseIntakeService
{
    public async Task<IReadOnlyList<AdminIntakeDatesDto>> ListForAdminAsync(int adminAccountId, CancellationToken ct = default)
    {
        await RequireAdminAccountAsync(adminAccountId, ct);
        return await db.CourseIntakes.AsNoTracking().OrderByDescending(i => i.Id)
            .Select(i => new AdminIntakeDatesDto(i.Id, i.CourseId, i.Course.Code, i.Course.Title,
                i.Trainer.FullName, i.StartsAt, i.EndsAt, i.Status.ToString())).ToListAsync(ct);
    }

    public async Task<CourseIntakeDetailDto> GetForAdminAsync(int adminAccountId, int courseIntakeId, CancellationToken ct = default)
        => ToDto(await LoadForAdminAsync(adminAccountId, courseIntakeId, ct));

    public async Task<CourseIntakeDetailDto> UpdateTeachingDatesForAdminAsync(int adminAccountId, int courseIntakeId,
        UpdateIntakeTeachingDatesRequest request, CancellationToken ct = default)
    {
        var intake = await LoadForAdminAsync(adminAccountId, courseIntakeId, ct);
        return await SaveTeachingDatesAsync(intake, request, adminAccountId, ct);
    }

    private async Task<CourseIntakeDetailDto> SaveTeachingDatesAsync(CourseIntake intake,
        UpdateIntakeTeachingDatesRequest request, int adminAccountId, CancellationToken ct)
    {
        CourseIntakeValidation.Version(intake, request.Version);
        if (intake.Status is CourseIntakeStatus.Completed or CourseIntakeStatus.Cancelled or CourseIntakeStatus.PendingApproval)
            throw new CourseIntakeException("INTAKE_NOT_EDITABLE", "Teaching dates cannot be changed for a completed, cancelled or pending Intake.", 409);
        CourseIntakeValidation.Utc(request.StartsAt, "startsAt");
        CourseIntakeValidation.Utc(request.EndsAt, "endsAt");
        if (request.StartsAt >= request.EndsAt)
            throw new CourseIntakeException("INVALID_INTAKE_DATES", "Teaching must end after it starts.", field: "endsAt");
        if (request.StartsAt < intake.RegistrationClosesAt)
            throw new CourseIntakeException("INVALID_INTAKE_DATES", "Teaching must start on or after registration closes.", field: "startsAt");
        if (intake.Sessions.Any(s => s.StartsAt < request.StartsAt || s.EndsAt > request.EndsAt))
            throw new CourseIntakeException("SESSION_OUTSIDE_INTAKE", "The teaching dates must include every existing session.", field: "endsAt");
        if (intake.StartsAt == request.StartsAt && intake.EndsAt == request.EndsAt) return ToDto(intake);

        var reason = $"Teaching dates: {DateTime.SpecifyKind(intake.StartsAt, DateTimeKind.Utc):O} / {DateTime.SpecifyKind(intake.EndsAt, DateTimeKind.Utc):O}"
            + $" -> {request.StartsAt:O} / {request.EndsAt:O}";
        // This operation adjusts the delivery window, including for enrolled/running classes.
        // It never moves sessions, changes registration or performs a completion transition.
        intake.StartsAt = request.StartsAt;
        intake.EndsAt = request.EndsAt;
        intake.Version = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog { AdminAccountId = adminAccountId,
            Action = "CourseIntakeTeachingDatesUpdated", EntityType = nameof(CourseIntake),
            EntityId = intake.Id.ToString(), Result = intake.Status.ToString(), Reason = reason });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new CourseIntakeException("INTAKE_VERSION_CONFLICT", "The Intake changed. Refresh and retry.", 409);
        }
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> PostponeAsync(int creatorUserId, int intakeId,
        CreateCourseIntakeRequest request, CancellationToken ct = default)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var original = await LoadForCreatorAsync(creatorUserId, intakeId, ct);
            var now = DateTime.UtcNow;
            if (original.Status != CourseIntakeStatus.Cancelled || original.CancellationReason != "MinimumEnrollmentNotMet"
                || original.CancelledAt is null || now > original.CancelledAt.Value.AddDays(7))
                throw new CourseIntakeException("POSTPONEMENT_NOT_ALLOWED", "An Intake cancelled for low enrollment can be replaced within seven days.", 409);
            if (original.ReplacementIntake is not null)
                throw new CourseIntakeException("POSTPONEMENT_ALREADY_CREATED", "This Intake already has a replacement.", 409);
            await RequirePublishedCourseAsync(original.CourseId, ct);
            await RequireAcceptedTrainerAsync(original.CourseId, original.TrainerId, ct);
            ValidateDates(request.RegistrationOpensAt, request.RegistrationClosesAt, request.StartsAt, request.EndsAt, request.DaysBeforeTeaching);
            ValidateMinEnrollment(request.MinEnrollment);
            if (request.StartsAt <= original.StartsAt || request.RegistrationClosesAt <= now)
                throw new CourseIntakeException("INVALID_POSTPONEMENT_DATES", "The replacement must start later and have an open future registration window.");
            var replacement = new CourseIntake { CourseId = original.CourseId, TrainerId = original.TrainerId,
                ReplacementForIntakeId = original.Id, Status = CourseIntakeStatus.Published,
                // The same Trainer already agreed to this run; the replacement only moves it later.
                TrainerInvitation = TrainerInvitationStatus.Accepted, TrainerInvitedAt = original.TrainerInvitedAt, TrainerRespondedAt = now,
                ConfirmedByCreatorId = creatorUserId, ConfirmedAt = now,
                RegistrationOpensAt = request.RegistrationOpensAt,
                RegistrationClosesAt = request.RegistrationClosesAt, StartsAt = request.StartsAt,
                EndsAt = request.EndsAt, MinEnrollment = request.MinEnrollment };
            var shift = request.StartsAt - original.StartsAt;
            foreach (var session in original.Sessions)
            {
                var copy = BuildSession(session.Label, session.StartsAt + shift, session.EndsAt + shift,
                    session.MeetingLink, session.PhysicalAddress, session.PhysicalCapacity,
                    session.PhysicalBookingDeadline + shift, replacement);
                ValidateCapacity(copy, replacement.MinEnrollment);
                replacement.Sessions.Add(copy);
            }
            if (replacement.Sessions.Count == 0)
                throw new CourseIntakeException("SESSION_REQUIRED", "The replacement needs at least one session.", field: "sessions");
            CourseIntakeValidation.TotalDuration(original.Course.DurationHours, replacement.Sessions);
            db.CourseIntakes.Add(replacement);
            original.Version = Guid.NewGuid();
            var learners = await db.Enrollments.Where(e => e.CourseSession.CourseIntakeId == original.Id
                && e.PostponementEligible && e.Status == EnrollmentStatus.Cancelled)
                .Select(e => new { e.UserId, e.Id }).ToListAsync(ct);
            await db.SaveChangesAsync(ct);
            foreach (var learner in learners)
                db.Notifications.Add(new Notification { UserId = learner.UserId, IntakeId = replacement.Id, EnrollmentId = learner.Id, EmailPending = true, Code = "N-postponement-offered",
                    Title = "Postponed class available", Body = $"Intake #{replacement.Id} replaces Intake #{original.Id}. Choose a session in My Programs to reserve again using your available credits." });
            await SaveAsync(replacement, creatorUserId, "CourseIntakePostponed", ct);
            await tx.CommitAsync(ct);
            return ToDto(replacement);
        });
    }

    public async Task<IReadOnlyList<CourseIntakeSummaryDto>> ListOwnedAsync(int trainerUserId, CancellationToken ct = default)
    {
        await RequireTrainerAsync(trainerUserId, ct);
        return await db.CourseIntakes.AsNoTracking().Where(i => i.TrainerId == trainerUserId).OrderByDescending(i => i.Id)
            .Select(i => new CourseIntakeSummaryDto(i.Id, i.CourseId, i.TrainerId, i.StartsAt, i.EndsAt, i.Status.ToString(), i.Version,
                i.TrainerInvitation.ToString())).ToListAsync(ct);
    }

    public async Task<CourseIntakeDetailDto> GetOwnedAsync(int trainerUserId, int courseIntakeId, CancellationToken ct = default)
        => ToDto(await LoadOwnedAsync(trainerUserId, courseIntakeId, ct));

    public async Task<IReadOnlyList<CourseIntakeSummaryDto>> ListForCreatorAsync(int creatorUserId, int? courseId, CancellationToken ct = default)
    {
        await RequireCreatorAsync(creatorUserId, ct);
        return await db.CourseIntakes.AsNoTracking()
            .Where(i => i.Course.CreatorId == creatorUserId && (courseId == null || i.CourseId == courseId))
            .OrderByDescending(i => i.Id)
            .Select(i => new CourseIntakeSummaryDto(i.Id, i.CourseId, i.TrainerId, i.StartsAt, i.EndsAt, i.Status.ToString(), i.Version,
                i.TrainerInvitation.ToString()))
            .ToListAsync(ct);
    }

    public async Task<CourseIntakeDetailDto> GetForCreatorAsync(int creatorUserId, int courseIntakeId, CancellationToken ct = default)
        => ToDto(await LoadForCreatorAsync(creatorUserId, courseIntakeId, ct));

    public async Task<CourseIntakeDetailDto> CreateAsync(int creatorUserId, int courseId, CreateCourseIntakeRequest request, CancellationToken ct = default)
    {
        var creationVersion = Guid.NewGuid();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await RequireCreatorAsync(creatorUserId, ct);
            var existing = await db.CourseIntakes
                .Include(item => item.Sessions).ThenInclude(session => session.Enrollments)
                .Include(item => item.Applications)
                .SingleOrDefaultAsync(item => item.CourseId == courseId && item.Version == creationVersion, ct);
            if (existing is not null)
                return ToDto(existing);

            await RequireOwnedPublishedCourseAsync(creatorUserId, courseId, ct);
            await RequireAcceptedTrainerAsync(courseId, request.TrainerId, ct);
            ValidateDates(request.RegistrationOpensAt, request.RegistrationClosesAt, request.StartsAt, request.EndsAt, request.DaysBeforeTeaching);
            ValidateMinEnrollment(request.MinEnrollment);
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var intake = new CourseIntake { CourseId = courseId, TrainerId = request.TrainerId,
                RegistrationOpensAt = request.RegistrationOpensAt, RegistrationClosesAt = request.RegistrationClosesAt,
                StartsAt = request.StartsAt, EndsAt = request.EndsAt, MinEnrollment = request.MinEnrollment,
                Version = creationVersion };
            db.CourseIntakes.Add(intake);
            await db.SaveChangesAsync(ct);
            await SaveWithCurrentVersionAsync(intake, creatorUserId, "CourseIntakeCreated", null, ct);
            await transaction.CommitAsync(ct);
            return ToDto(intake);
        });
    }

    public async Task<CourseIntakeDetailDto> UpdateAsync(int creatorUserId, int courseIntakeId, UpdateCourseIntakeRequest request, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(creatorUserId, courseIntakeId, request.Version, ct);
        // Registration that already opened keeps its opening time; only a new opening time must be in the future.
        ValidateDates(request.RegistrationOpensAt, request.RegistrationClosesAt, request.StartsAt, request.EndsAt, request.DaysBeforeTeaching,
            utcNow: request.RegistrationOpensAt == intake.RegistrationOpensAt ? intake.RegistrationOpensAt : null);
        if (request.RegistrationClosesAt <= DateTime.UtcNow)
            throw new CourseIntakeException("REGISTRATION_ALREADY_CLOSED",
                "Registration must close in the future.", field: "registrationClosesAt");
        var minEnrollment = request.MinEnrollment ?? intake.MinEnrollment;
        ValidateMinEnrollment(minEnrollment);
        foreach (var session in intake.Sessions)
        {
            CourseIntakeValidation.Session(session, request.StartsAt, request.EndsAt);
            ValidateCapacity(session, minEnrollment);
        }
        intake.RegistrationOpensAt = request.RegistrationOpensAt;
        intake.RegistrationClosesAt = request.RegistrationClosesAt;
        intake.StartsAt = request.StartsAt;
        intake.EndsAt = request.EndsAt;
        intake.MinEnrollment = minEnrollment;
        if (request.TrainerId is { } trainerId && trainerId != intake.TrainerId)
        {
            await RequireAcceptedTrainerAsync(intake.CourseId, trainerId, ct);
            intake.TrainerId = trainerId;
            intake.TrainerInvitation = TrainerInvitationStatus.NotSent;
            intake.TrainerInvitedAt = null;
            intake.TrainerRespondedAt = null;
            intake.TrainerDeclineReason = null;
        }
        KeepScheduleStatus(intake);
        await SaveAsync(intake, creatorUserId, "CourseIntakeUpdated", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> AddSessionAsync(int creatorUserId, int courseIntakeId, CreateCourseSessionRequest request, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(creatorUserId, courseIntakeId, request.Version, ct);
        var session = BuildSession(request.Label, request.StartsAt, request.EndsAt, request.MeetingLink,
            request.PhysicalAddress, request.PhysicalCapacity, request.PhysicalBookingDeadline, intake);
        ValidateCapacity(session, intake.MinEnrollment);
        intake.Sessions.Add(session);
        FitTeachingDates(intake);
        KeepScheduleStatus(intake);
        await SaveAsync(intake, creatorUserId, "CourseSessionAdded", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> UpdateSessionAsync(int creatorUserId, int courseIntakeId, int courseSessionId, UpdateCourseSessionRequest request, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(creatorUserId, courseIntakeId, request.Version, ct);
        var session = FindSession(intake, courseSessionId);
        var next = BuildSession(request.Label, request.StartsAt, request.EndsAt, request.MeetingLink,
            request.PhysicalAddress, request.PhysicalCapacity, request.PhysicalBookingDeadline, intake);
        ValidateCapacity(next, intake.MinEnrollment);
        await RequireNoHistoryAsync(session.Id, ct);
        if (intake.Status == CourseIntakeStatus.Published)
            CourseIntakeValidation.TotalDuration(intake.Course.DurationHours, intake.Sessions.Select(item => item.Id == session.Id ? next : item));
        session.Label = next.Label;
        session.StartsAt = next.StartsAt;
        session.EndsAt = next.EndsAt;
        session.MeetingLink = next.MeetingLink;
        session.PhysicalAddress = next.PhysicalAddress;
        session.PhysicalCapacity = next.PhysicalCapacity;
        session.PhysicalBookingDeadline = next.PhysicalBookingDeadline;
        FitTeachingDates(intake);
        KeepScheduleStatus(intake);
        await SaveAsync(intake, creatorUserId, "CourseSessionUpdated", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> DeleteSessionAsync(int creatorUserId, int courseIntakeId, int courseSessionId, Guid version, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(creatorUserId, courseIntakeId, version, ct);
        var session = FindSession(intake, courseSessionId);
        await RequireNoHistoryAsync(session.Id, ct);
        if (intake.Status == CourseIntakeStatus.Published)
            CourseIntakeValidation.TotalDuration(intake.Course.DurationHours, intake.Sessions.Where(item => item.Id != session.Id));
        db.CourseSessions.Remove(session);
        intake.Sessions.Remove(session);
        FitTeachingDates(intake);
        KeepScheduleStatus(intake);
        await SaveAsync(intake, creatorUserId, "CourseSessionDeleted", ct);
        return ToDto(intake);
    }

    // The Creator says when the class meets. Registration opens now (or at a chosen time) and closes
    // a number of days before the first session; teaching runs from the first session to the last.
    public async Task<CourseIntakeDetailDto> OpenClassAsync(int creatorUserId, int courseId, OpenClassRequest request, CancellationToken ct = default)
    {
        var starts = request.SessionStarts ?? [];
        if (starts.Count is < 1 or > 60)
            throw new CourseIntakeException("INVALID_SESSION_SCHEDULE", "Add between 1 and 60 sessions.", field: "sessionStarts");
        if (request.SessionMinutes is < 30 or > 720)
            throw new CourseIntakeException("INVALID_SESSION_SCHEDULE", "A session lasts between 30 minutes and 12 hours.", field: "sessionMinutes");
        if (request.RegistrationClosesDaysBefore is < 0 or > 60)
            throw new CourseIntakeException("INVALID_INTAKE_DATES", "Registration closes 0 to 60 days before the first session.", field: "registrationClosesDaysBefore");
        foreach (var start in starts) CourseIntakeValidation.Utc(start, "sessionStarts");
        var length = TimeSpan.FromMinutes(request.SessionMinutes);
        for (var index = 1; index < starts.Count; index++)
        {
            if (starts[index] < starts[index - 1] + length)
                throw new CourseIntakeException("INVALID_SESSION_SCHEDULE", "Sessions must be in order and must not overlap.", field: "sessionStarts");
        }
        var meetingLink = string.IsNullOrWhiteSpace(request.MeetingLink) ? null : request.MeetingLink.Trim();
        var physicalAddress = string.IsNullOrWhiteSpace(request.PhysicalAddress) ? null : request.PhysicalAddress.Trim();
        if (meetingLink is null && physicalAddress is null)
            throw new CourseIntakeException("DELIVERY_LOCATION_REQUIRED", "Provide an online meeting link or a physical location.", field: "meetingLink");
        ValidateMinEnrollment(request.MinEnrollment);

        var now = DateTime.UtcNow;
        var opens = request.RegistrationOpensAt is { } chosen && chosen > now ? chosen : now;
        if (request.RegistrationOpensAt is not null) CourseIntakeValidation.Utc(request.RegistrationOpensAt.Value, "registrationOpensAt");
        var closes = starts[0].AddDays(-request.RegistrationClosesDaysBefore);
        if (closes <= opens)
        {
            var earliest = opens.AddDays(request.RegistrationClosesDaysBefore);
            throw new CourseIntakeException("FIRST_SESSION_TOO_SOON",
                $"Registration needs time to run. The first session can start after {earliest:yyyy-MM-dd HH:mm} UTC, or close registration fewer days before it.",
                field: "sessionStarts");
        }

        var teachingStart = starts[0];
        var teachingEnd = starts[^1] + length;
        var bookingDeadline = physicalAddress is null ? (DateTime?)null : closes;
        var proposed = new List<CourseSession>();
        for (var index = 0; index < starts.Count; index++)
        {
            var prototype = new CourseSession
            {
                Label = $"Session {index + 1}",
                StartsAt = starts[index],
                EndsAt = starts[index] + length,
                MeetingLink = meetingLink,
                PhysicalAddress = physicalAddress,
                PhysicalCapacity = physicalAddress is null ? 0 : request.PhysicalCapacity,
                PhysicalBookingDeadline = bookingDeadline,
            };
            CourseIntakeValidation.Session(prototype, teachingStart, teachingEnd);
            ValidateCapacity(prototype, request.MinEnrollment);
            proposed.Add(prototype);
        }

        if (request.Publish)
            throw new CourseIntakeException("TRAINER_NOT_CONFIRMED",
                "Send the schedule to the Trainer and wait for them to accept before publishing.", 409);
        if (request.SendToTrainer)
        {
            await RequireCreatorAsync(creatorUserId, ct);
            var course = await RequireOwnedPublishedCourseAsync(creatorUserId, courseId, ct);
            CourseIntakeValidation.TotalDuration(course.DurationHours, proposed);
        }
        var intake = await CreateAsync(creatorUserId, courseId, new CreateCourseIntakeRequest(
            opens, closes, starts[0], starts[^1] + length, request.TrainerId, request.MinEnrollment), ct);
        for (var index = 0; index < starts.Count; index++)
        {
            intake = await AddSessionAsync(creatorUserId, intake.Id, new CreateCourseSessionRequest(
                $"Session {index + 1}", starts[index], starts[index] + length, meetingLink, physicalAddress,
                physicalAddress is null ? 0 : request.PhysicalCapacity, physicalAddress is null ? null : closes, intake.Version), ct);
        }
        return request.SendToTrainer ? await SendToTrainerAsync(creatorUserId, intake.Id, intake.Version, ct) : intake;
    }

    public async Task<CourseIntakeDetailDto> PublishAsync(int creatorUserId, int courseIntakeId, Guid version, CancellationToken ct = default)
    {
        var intake = await LoadForCreatorAsync(creatorUserId, courseIntakeId, ct);
        CourseIntakeValidation.Version(intake, version);
        if (intake.Status == CourseIntakeStatus.Published) return ToDto(intake);
        if (intake.Status is not (CourseIntakeStatus.Draft or CourseIntakeStatus.Rejected))
            throw new CourseIntakeException("INTAKE_NOT_PUBLISHABLE", "Only a draft Intake can be published.", 409);
        await RequirePublishedCourseAsync(intake.CourseId, ct);
        await RequireAcceptedTrainerAsync(intake.CourseId, intake.TrainerId, ct);
        // A draft may have been prepared earlier. Registration then opens at the moment of publishing.
        var publishedAt = DateTime.UtcNow;
        if (intake.RegistrationClosesAt <= publishedAt)
            throw new CourseIntakeException("REGISTRATION_ALREADY_CLOSED",
                "Registration would already be closed. Move the registration dates into the future.", 409, "registrationClosesAt");
        if (intake.RegistrationOpensAt < publishedAt)
            intake.RegistrationOpensAt = publishedAt;
        CourseIntakeValidation.Dates(intake.RegistrationOpensAt, intake.RegistrationClosesAt, intake.StartsAt, intake.EndsAt);
        if (intake.Sessions.Count == 0)
            throw new CourseIntakeException("SESSION_REQUIRED", "Add at least one valid session before publishing.", field: "sessions");
        foreach (var session in intake.Sessions)
        {
            CourseIntakeValidation.Session(session, intake.StartsAt, intake.EndsAt);
            ValidateCapacity(session, intake.MinEnrollment);
        }
        CourseIntakeValidation.TotalDuration(intake.Course.DurationHours, intake.Sessions);
        if (intake.TrainerInvitation != TrainerInvitationStatus.Accepted)
            throw new CourseIntakeException("TRAINER_NOT_CONFIRMED", "The Trainer must accept the schedule before the Intake is published.", 409);
        var now = DateTime.UtcNow;
        intake.Status = CourseIntakeStatus.Published;
        intake.ConfirmedByCreatorId = creatorUserId;
        intake.ConfirmedAt = now;
        intake.SubmittedAt = now;
        Notification? notification = null;
        if (intake.TrainerId != creatorUserId)
        {
            notification = new Notification
            {
                UserId = intake.TrainerId, IntakeId = intake.Id, EmailPending = true, Code = "N-intake-published",
                Title = "Intake published",
                Body = $"Intake #{intake.Id} for {intake.Course.Code} {intake.Course.Title}, which you accepted, is now open for registration.",
            };
            db.Notifications.Add(notification);
        }
        await SaveWithCurrentVersionAsync(intake, creatorUserId, "CourseIntakePublished", null, ct);
        if (notification is not null && realtime is not null)
            await realtime.PublishAsync(notification, ct);
        return ToDto(intake);
    }

    // The Trainer receives the whole fixed timetable and decides whether they can teach it.
    public async Task<CourseIntakeDetailDto> SendToTrainerAsync(int creatorUserId, int courseIntakeId, Guid version, CancellationToken ct = default)
    {
        var intake = await LoadForCreatorAsync(creatorUserId, courseIntakeId, ct);
        CourseIntakeValidation.Version(intake, version);
        if (intake.TrainerInvitation is TrainerInvitationStatus.Invited or TrainerInvitationStatus.Accepted) return ToDto(intake);
        if (intake.Status is not (CourseIntakeStatus.Draft or CourseIntakeStatus.Rejected))
            throw new CourseIntakeException("INTAKE_NOT_EDITABLE", "Only a draft Intake can be sent to a Trainer.", 409);
        await RequirePublishedCourseAsync(intake.CourseId, ct);
        await RequireAcceptedTrainerAsync(intake.CourseId, intake.TrainerId, ct);
        var now = DateTime.UtcNow;
        if (intake.RegistrationClosesAt <= now)
            throw new CourseIntakeException("REGISTRATION_ALREADY_CLOSED",
                "Registration would already be closed. Move the schedule into the future.", 409, "registrationClosesAt");
        if (intake.Sessions.Count == 0)
            throw new CourseIntakeException("SESSION_REQUIRED", "Add every session before sending the schedule.", field: "sessions");
        foreach (var session in intake.Sessions)
        {
            CourseIntakeValidation.Session(session, intake.StartsAt, intake.EndsAt);
            ValidateCapacity(session, intake.MinEnrollment);
        }
        CourseIntakeValidation.TotalDuration(intake.Course.DurationHours, intake.Sessions);

        intake.TrainerInvitation = TrainerInvitationStatus.Invited;
        intake.TrainerInvitedAt = now;
        intake.TrainerRespondedAt = null;
        intake.TrainerDeclineReason = null;
        var creatorName = await db.Users.Where(u => u.Id == creatorUserId).Select(u => u.FullName).SingleAsync(ct);
        var first = intake.Sessions.Min(s => s.StartsAt);
        var last = intake.Sessions.Max(s => s.EndsAt);
        var count = intake.Sessions.Count;
        var notification = new Notification
        {
            UserId = intake.TrainerId, IntakeId = intake.Id, EmailPending = true, Code = "N-intake-invitation",
            Title = "Intake teaching invitation",
            Body = $"{creatorName} invited you to teach {intake.Course.Code} {intake.Course.Title} (Intake #{intake.Id}): "
                + $"{count} {(count == 1 ? "session" : "sessions")} from {first:yyyy-MM-dd} to {last:yyyy-MM-dd} (UTC). Check the schedule and reply.",
        };
        db.Notifications.Add(notification);
        await SaveAsync(intake, creatorUserId, "CourseIntakeTrainerInvited", ct);
        if (realtime is not null) await realtime.PublishAsync(notification, ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> WithdrawTrainerInvitationAsync(int creatorUserId, int courseIntakeId, Guid version, CancellationToken ct = default)
    {
        var intake = await LoadForCreatorAsync(creatorUserId, courseIntakeId, ct);
        CourseIntakeValidation.Version(intake, version);
        if (intake.Status is CourseIntakeStatus.Published or CourseIntakeStatus.InProgress or CourseIntakeStatus.Completed)
            throw new CourseIntakeException("INTAKE_ALREADY_PUBLISHED", "A published Intake keeps its Trainer and schedule.", 409);
        if (intake.Status is not (CourseIntakeStatus.Draft or CourseIntakeStatus.Rejected))
            throw new CourseIntakeException("INTAKE_NOT_EDITABLE", "This Intake can no longer be changed.", 409);
        if (intake.TrainerInvitation is not (TrainerInvitationStatus.Invited or TrainerInvitationStatus.Accepted)) return ToDto(intake);

        intake.TrainerInvitation = TrainerInvitationStatus.NotSent;
        intake.TrainerRespondedAt = null;
        intake.TrainerDeclineReason = null;
        db.Notifications.Add(new Notification
        {
            UserId = intake.TrainerId, IntakeId = intake.Id, Code = "N-intake-invitation-withdrawn",
            Title = "Intake invitation withdrawn",
            Body = $"The invitation to teach {intake.Course.Code} {intake.Course.Title} (Intake #{intake.Id}) was withdrawn. The schedule may change.",
        });
        await SaveAsync(intake, creatorUserId, "CourseIntakeTrainerInvitationWithdrawn", ct);
        return ToDto(intake);
    }

    private async Task RequireTrainerAsync(int userId, CancellationToken ct)
    {
        if (userId <= 0) throw new CourseIntakeException("UNAUTHENTICATED", "An authenticated User is required.", 401);
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Roles.Any(r => r.Role == AppRole.Trainer), ct))
            throw new CourseIntakeException("TRAINER_REQUIRED", "An active Trainer account is required.", 403);
    }

    private async Task RequireCreatorAsync(int userId, CancellationToken ct)
    {
        if (userId <= 0) throw new CourseIntakeException("UNAUTHENTICATED", "An authenticated User is required.", 401);
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Roles.Any(r => r.Role == AppRole.Creator), ct))
            throw new CourseIntakeException("CREATOR_REQUIRED", "An active Creator account is required.", 403);
    }

    private async Task RequirePublishedCourseAsync(int courseId, CancellationToken ct)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == courseId && c.Status == CourseStatus.Published, ct))
            throw new CourseIntakeException("COURSE_NOT_PUBLISHED", "The Course must be published.", 409);
    }

    private async Task<Course> RequireOwnedPublishedCourseAsync(int creatorUserId, int courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == courseId && c.CreatorId == creatorUserId, ct)
            ?? throw new CourseIntakeException("COURSE_NOT_FOUND", "Course was not found.", 404);
        if (course.Status != CourseStatus.Published)
            throw new CourseIntakeException("COURSE_NOT_PUBLISHED", "The Course must be published.", 409);
        return course;
    }

    private async Task RequireAcceptedTrainerAsync(int courseId, int trainerId, CancellationToken ct)
    {
        // Trainers who applied to the course may also be invited; they join it by accepting an Intake's schedule.
        var accepted = await db.CourseTrainers.AnyAsync(item => item.CourseId == courseId && item.TrainerId == trainerId
            && (item.Status == CourseTrainerStatus.Accepted || item.Status == CourseTrainerStatus.Requested)
            && item.Trainer.IsActive && item.Trainer.Roles.Any(role => role.Role == AppRole.Trainer), ct);
        if (!accepted)
            throw new CourseIntakeException("TRAINER_NOT_ACCEPTED", "Choose a Trainer who has joined or applied to this course.", 409, "trainerId");
    }

    private async Task<CourseIntake> LoadOwnedAsync(int userId, int id, CancellationToken ct)
    {
        await RequireTrainerAsync(userId, ct);
        return await db.CourseIntakes.Include(i => i.Sessions).ThenInclude(session => session.Enrollments)
            .Include(i => i.Applications).Include(i => i.ReplacementIntake).Include(i => i.Trainer).AsSplitQuery()
            .SingleOrDefaultAsync(i => i.Id == id && i.TrainerId == userId, ct)
            ?? throw new CourseIntakeException("INTAKE_NOT_FOUND", "Owned Intake was not found.", 404);
    }

    private async Task<CourseIntake> LoadForCreatorAsync(int userId, int id, CancellationToken ct)
    {
        await RequireCreatorAsync(userId, ct);
        return await db.CourseIntakes.Include(i => i.Sessions).ThenInclude(session => session.Enrollments)
            .Include(i => i.Applications).Include(i => i.ReplacementIntake).AsSplitQuery()
            .Include(i => i.Course).Include(i => i.Trainer)
            .SingleOrDefaultAsync(i => i.Id == id && i.Course.CreatorId == userId, ct)
            ?? throw new CourseIntakeException("INTAKE_NOT_FOUND", "Owned Intake was not found.", 404);
    }

    private async Task RequireAdminAccountAsync(int adminAccountId, CancellationToken ct)
    {
        if (!await db.AdminAccounts.AnyAsync(a => a.Id == adminAccountId && a.IsActive, ct))
            throw new CourseIntakeException("ADMIN_REQUIRED", "An active administrator account is required.", 403);
    }

    private async Task<CourseIntake> LoadForAdminAsync(int adminAccountId, int id, CancellationToken ct)
    {
        await RequireAdminAccountAsync(adminAccountId, ct);
        return await db.CourseIntakes.Include(i => i.Sessions).ThenInclude(s => s.Enrollments)
            .Include(i => i.Applications).Include(i => i.ReplacementIntake).Include(i => i.Trainer).AsSplitQuery()
            .SingleOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new CourseIntakeException("INTAKE_NOT_FOUND", "Intake was not found.", 404);
    }

    private async Task<CourseIntake> LoadEditableAsync(int userId, int id, Guid version, CancellationToken ct)
    {
        var intake = await LoadForCreatorAsync(userId, id, ct);
        CourseIntakeValidation.Version(intake, version);
        CourseIntakeValidation.Editable(intake);
        return intake;
    }

    private async Task RequireNoHistoryAsync(int id, CancellationToken ct)
    {
        if (await db.SessionMaterials.AnyAsync(material => material.CourseSessionId == id, ct))
            throw new CourseIntakeException("SESSION_HAS_MATERIALS",
                "A session with uploaded materials cannot be structurally changed or deleted.", 409);
        if (await db.Enrollments.AnyAsync(e => e.CourseSessionId == id, ct)
            || await db.AttendanceRecords.AnyAsync(a => a.CourseSessionId == id, ct)
            || await db.CourseSessions.AnyAsync(s => s.Id == id && s.SeatsTaken > 0, ct))
            throw new CourseIntakeException("SESSION_HAS_HISTORY", "A session with enrolment or attendance history cannot be structurally changed or deleted.", 409);
    }

    private static CourseSession FindSession(CourseIntake intake, int id)
        => intake.Sessions.SingleOrDefault(s => s.Id == id)
            ?? throw new CourseIntakeException("SESSION_NOT_FOUND", "Session was not found in this Intake.", 404);

    private static CourseSession BuildSession(string label, DateTime starts, DateTime ends, string? link,
        string? address, int capacity, DateTime? deadline, CourseIntake intake)
    {
        CourseIntakeValidation.Utc(starts, "startsAt");
        CourseIntakeValidation.Utc(ends, "endsAt");
        if (deadline.HasValue) CourseIntakeValidation.Utc(deadline.Value, "physicalBookingDeadline");
        var session = new CourseSession { CourseIntakeId = intake.Id, Label = label?.Trim() ?? "", StartsAt = starts, EndsAt = ends,
            MeetingLink = string.IsNullOrWhiteSpace(link) ? null : link.Trim(), PhysicalAddress = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            PhysicalCapacity = capacity, PhysicalBookingDeadline = deadline };
        if (starts < intake.RegistrationClosesAt)
            throw new CourseIntakeException("SESSION_BEFORE_REGISTRATION_CLOSES",
                "A session cannot start before registration closes.", field: "startsAt");
        CourseIntakeValidation.Session(session, Earlier(intake.StartsAt, starts), Later(intake.EndsAt, ends));
        return session;
    }

    private static DateTime Earlier(DateTime left, DateTime right) => left <= right ? left : right;
    private static DateTime Later(DateTime left, DateTime right) => left >= right ? left : right;

    // Teaching dates are the first session start and the last session end. Adding, moving, or deleting a session refits them.
    private static void FitTeachingDates(CourseIntake intake)
    {
        if (intake.Sessions.Count == 0) return;
        intake.StartsAt = intake.Sessions.Min(session => session.StartsAt);
        intake.EndsAt = intake.Sessions.Max(session => session.EndsAt);
    }


    private static void ValidateDates(DateTime opens, DateTime closes, DateTime starts, DateTime ends, int daysBeforeTeaching = 0,
        DateTime? utcNow = null)
    {
        CourseIntakeValidation.Utc(opens, "registrationOpensAt");
        CourseIntakeValidation.Utc(closes, "registrationClosesAt");
        CourseIntakeValidation.Utc(starts, "startsAt");
        CourseIntakeValidation.Utc(ends, "endsAt");
        CourseIntakeValidation.Dates(opens, closes, starts, ends, daysBeforeTeaching, utcNow);
    }

    private static void ValidateMinEnrollment(int value)
    {
        if (value is < 2 or > 200)
            throw new CourseIntakeException("INVALID_MIN_ENROLLMENT", "Minimum enrollment must be 2 to 200.", field: "minEnrollment");
    }

    private static void ValidateCapacity(CourseSession session, int minEnrollment)
    {
        if (session.PhysicalAddress is not null && session.PhysicalCapacity < minEnrollment)
            throw new CourseIntakeException("CAPACITY_BELOW_MIN_ENROLLMENT",
                "Physical capacity must be at least the minimum enrollment.", field: "physicalCapacity");
    }

    private static void KeepScheduleStatus(CourseIntake intake)
    {
        if (intake.Status is not (CourseIntakeStatus.Draft or CourseIntakeStatus.Rejected)) return;
        intake.Status = CourseIntakeStatus.Draft;
        intake.SubmittedAt = null;
        intake.ConfirmedByCreatorId = null;
        intake.ConfirmedAt = null;
        intake.ConfirmationNote = null;
    }

    private async Task SaveAsync(CourseIntake intake, int actorUserId, string action, CancellationToken ct)
    {
        intake.Version = Guid.NewGuid();
        await SaveWithCurrentVersionAsync(intake, actorUserId, action, null, ct);
    }

    private async Task SaveWithCurrentVersionAsync(CourseIntake intake, int actorUserId, string action, string? reason, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog { UserId = actorUserId, Action = action, EntityType = nameof(CourseIntake),
            EntityId = intake.Id.ToString(), Result = intake.Status.ToString(), Reason = reason });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new CourseIntakeException("INTAKE_VERSION_CONFLICT", "The Intake changed concurrently. Reload before retrying.", 409);
        }
    }

    internal static CourseIntakeDetailDto ToDto(CourseIntake i)
    {
        var enrollments = i.Sessions.SelectMany(session => session.Enrollments);
        var reserved = enrollments.Count(item => item.Status == EnrollmentStatus.Reserved);
        var active = enrollments.Count(item => item.Status == EnrollmentStatus.Active);
        var remaining = Math.Max(0, i.MinEnrollment - reserved - active);
        return new(i.Id, i.CourseId, i.TrainerId, i.RegistrationOpensAt, i.RegistrationClosesAt, i.StartsAt, i.EndsAt,
            i.Status.ToString(), i.SubmittedAt, i.ConfirmedByCreatorId, i.ConfirmedAt, i.ConfirmationNote, i.Version,
            i.Sessions.OrderBy(s => s.StartsAt).ThenBy(s => s.Id).Select(s => new CourseSessionDto(s.Id, s.Label, s.StartsAt,
                s.EndsAt, s.PhysicalCapacity, s.SeatsLeft, s.CourseIntakeId, s.MeetingLink, s.PhysicalAddress, s.PhysicalCapacity, s.PhysicalBookingDeadline)).ToList(),
            i.Applications.Where(a => a.Kind == CourseIntakeApplicationKind.Change).OrderByDescending(a => a.Id)
                .Select(ToChangeDto).FirstOrDefault(), i.MinEnrollment, i.ConfirmedToRunAt, i.CancelledAt,
            i.CancellationReason, i.ReplacementForIntakeId, i.ReplacementIntake?.Id,
            i.CancellationReason == "MinimumEnrollmentNotMet" ? i.CancelledAt?.AddDays(7) : null,
            reserved, active, remaining, i.Trainer?.FullName, i.TrainerInvitation.ToString(), i.TrainerInvitedAt,
            i.TrainerRespondedAt, i.TrainerDeclineReason);
    }

    private static CourseIntakeChangeDto ToChangeDto(CourseIntakeApplication application)
    {
        var snapshot = ReadSnapshot(application);
        return new CourseIntakeChangeDto(application.Id, application.Status.ToString(), application.RestoreStatus?.ToString() ?? "Published",
            application.SubmittedAt, application.ReviewNote, snapshot.RegistrationOpensAt, snapshot.RegistrationClosesAt,
            snapshot.StartsAt, snapshot.EndsAt, snapshot.Sessions.Select(s => new ProposedCourseSessionRequest(s.Id, s.Label,
                s.StartsAt, s.EndsAt, s.MeetingLink, s.PhysicalAddress, s.PhysicalCapacity, s.PhysicalBookingDeadline)).ToList());
    }


    private static ChangeSnapshot ReadSnapshot(CourseIntakeApplication application)
    {
        try
        {
            return JsonSerializer.Deserialize<ChangeSnapshot>(application.ProposalJson ?? "")
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new CourseIntakeException("INTAKE_SAVE_FAILED", "The stored change proposal is invalid.", 500);
        }
    }

    private static SessionSnapshot ToSnapshot(CourseSession session)
        => new(session.Id > 0 ? session.Id : null, session.Label, session.StartsAt, session.EndsAt, session.MeetingLink,
            session.PhysicalAddress, session.PhysicalCapacity, session.PhysicalBookingDeadline);

    private static CourseSession FromSnapshot(SessionSnapshot session)
        => new() { Id = session.Id ?? 0, Label = session.Label, StartsAt = session.StartsAt, EndsAt = session.EndsAt,
            MeetingLink = session.MeetingLink, PhysicalAddress = session.PhysicalAddress, PhysicalCapacity = session.PhysicalCapacity,
            PhysicalBookingDeadline = session.PhysicalBookingDeadline };

    private sealed record ChangeSnapshot(DateTime RegistrationOpensAt, DateTime RegistrationClosesAt,
        DateTime StartsAt, DateTime EndsAt, IReadOnlyList<SessionSnapshot> Sessions);
    private sealed record SessionSnapshot(int? Id, string Label, DateTime StartsAt, DateTime EndsAt,
        string? MeetingLink, string? PhysicalAddress, int PhysicalCapacity, DateTime? PhysicalBookingDeadline);
}
