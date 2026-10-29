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
    Task<IReadOnlyList<CourseIntakeDetailDto>> ListSchedulesForAdminAsync(int adminAccountId, int courseId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> UpdateTeachingDatesForAdminAsync(int adminAccountId, int courseId, int intakeId, UpdateTeachingDatesRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> UpdateTeachingDatesForStaffAsync(int staffAccountId, int intakeId, UpdateTeachingDatesRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CourseIntakeSummaryDto>> ListOwnedAsync(int trainerUserId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> GetOwnedAsync(int trainerUserId, int courseIntakeId, CancellationToken ct = default);
    Task<IReadOnlyList<CourseIntakeSummaryDto>> ListForCreatorAsync(int creatorUserId, int? courseId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> GetForCreatorAsync(int creatorUserId, int courseIntakeId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> CreateAsync(int creatorUserId, int courseId, CreateCourseIntakeRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> CreateForStaffAsync(int staffAccountId, int courseId, CreateCourseIntakeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CourseIntakeSummaryDto>> ListForStaffAsync(int staffAccountId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> GetForStaffAsync(int staffAccountId, int courseIntakeId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> AddSessionForStaffAsync(int staffAccountId, int courseIntakeId, CreateCourseSessionRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> PublishForStaffAsync(int staffAccountId, int courseIntakeId, Guid version, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> PostponeAsync(int creatorUserId, int intakeId, CreateCourseIntakeRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> UpdateAsync(int creatorUserId, int courseIntakeId, UpdateCourseIntakeRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> AddSessionAsync(int creatorUserId, int courseIntakeId, CreateCourseSessionRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> UpdateSessionAsync(int creatorUserId, int courseIntakeId, int courseSessionId, UpdateCourseSessionRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> DeleteSessionAsync(int creatorUserId, int courseIntakeId, int courseSessionId, Guid version, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> PublishAsync(int creatorUserId, int courseIntakeId, Guid version, CancellationToken ct = default);
}

public sealed partial class CourseIntakeService(CoLearnXDbContext db, INotificationRealtime? realtime = null) : ICourseIntakeService
{
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
                throw new CourseIntakeException("POSTPONEMENT_NOT_ALLOWED", "A class cancelled for low enrollment can be replaced within seven days.", 409);
            if (original.ReplacementIntake is not null)
                throw new CourseIntakeException("POSTPONEMENT_ALREADY_CREATED", "This class already has a replacement Intake.", 409);
            await RequirePublishedCourseAsync(original.CourseId, ct);
            await RequireAcceptedTrainerAsync(original.CourseId, original.TrainerId, ct);
            ValidateDates(request.RegistrationOpensAt, request.RegistrationClosesAt, request.StartsAt, request.EndsAt, request.DaysBeforeTeaching);
            ValidateMinEnrollment(request.MinEnrollment);
            if (request.StartsAt <= original.StartsAt || request.RegistrationClosesAt <= now)
                throw new CourseIntakeException("INVALID_POSTPONEMENT_DATES", "The replacement must start later and have an open future registration window.");
            var replacement = new CourseIntake { CourseId = original.CourseId, TrainerId = original.TrainerId,
                ReplacementForIntakeId = original.Id, Status = CourseIntakeStatus.Published,
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
            .Select(i => new CourseIntakeSummaryDto(i.Id, i.CourseId, i.TrainerId, i.StartsAt, i.EndsAt, i.Status.ToString(), i.Version)).ToListAsync(ct);
    }

    public async Task<CourseIntakeDetailDto> GetOwnedAsync(int trainerUserId, int courseIntakeId, CancellationToken ct = default)
        => ToDto(await LoadOwnedAsync(trainerUserId, courseIntakeId, ct));

    public async Task<IReadOnlyList<CourseIntakeSummaryDto>> ListForCreatorAsync(int creatorUserId, int? courseId, CancellationToken ct = default)
    {
        await RequireCreatorAsync(creatorUserId, ct);
        return await db.CourseIntakes.AsNoTracking()
            .Where(i => i.Course.CreatorId == creatorUserId && (courseId == null || i.CourseId == courseId))
            .OrderByDescending(i => i.Id)
            .Select(i => new CourseIntakeSummaryDto(i.Id, i.CourseId, i.TrainerId, i.StartsAt, i.EndsAt, i.Status.ToString(), i.Version))
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

    public async Task<CourseIntakeDetailDto> CreateForStaffAsync(
        int staffAccountId,
        int courseId,
        CreateCourseIntakeRequest request,
        CancellationToken ct = default)
    {
        var staff = await RequireStaffAccountAsync(staffAccountId, ct);
        var course = await db.Courses.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == courseId && item.IsPlatformOwned
                && item.TrainerId == staff.UserId, ct)
            ?? throw new CourseIntakeException("COURSE_NOT_FOUND", "Course was not found.", 404);
        if (course.Status != CourseStatus.Published)
            throw new CourseIntakeException("COURSE_NOT_PUBLISHED", "The Course must be published.", 409);
        await RequireAcceptedTrainerAsync(courseId, staff.UserId, ct);
        ValidateDates(request.RegistrationOpensAt, request.RegistrationClosesAt, request.StartsAt,
            request.EndsAt, request.DaysBeforeTeaching);
        ValidateMinEnrollment(request.MinEnrollment);
        var intake = new CourseIntake
        {
            CourseId = courseId,
            TrainerId = staff.UserId,
            RegistrationOpensAt = request.RegistrationOpensAt,
            RegistrationClosesAt = request.RegistrationClosesAt,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            MinEnrollment = request.MinEnrollment,
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.CourseIntakes.Add(intake);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog
        {
            StaffAccountId = staffAccountId,
            Action = "CourseIntakeCreated",
            EntityType = nameof(CourseIntake),
            EntityId = intake.Id.ToString(),
            Result = intake.Status.ToString(),
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToDto(intake);
    }

    public async Task<IReadOnlyList<CourseIntakeSummaryDto>> ListForStaffAsync(
        int staffAccountId,
        CancellationToken ct = default)
    {
        var staff = await RequireStaffAccountAsync(staffAccountId, ct);
        return await db.CourseIntakes.AsNoTracking()
            .Where(item => item.TrainerId == staff.UserId && item.Course.IsPlatformOwned)
            .OrderByDescending(item => item.Id)
            .Select(item => new CourseIntakeSummaryDto(item.Id, item.CourseId, item.TrainerId,
                item.StartsAt, item.EndsAt, item.Status.ToString(), item.Version))
            .ToListAsync(ct);
    }

    public async Task<CourseIntakeDetailDto> GetForStaffAsync(
        int staffAccountId,
        int courseIntakeId,
        CancellationToken ct = default)
        => ToDto(await LoadForStaffAsync(staffAccountId, courseIntakeId, ct));

    public async Task<CourseIntakeDetailDto> AddSessionForStaffAsync(
        int staffAccountId,
        int courseIntakeId,
        CreateCourseSessionRequest request,
        CancellationToken ct = default)
    {
        var intake = await LoadForStaffAsync(staffAccountId, courseIntakeId, ct);
        CourseIntakeValidation.Version(intake, request.Version);
        CourseIntakeValidation.Editable(intake);
        var session = BuildSession(request.Label, request.StartsAt, request.EndsAt, request.MeetingLink,
            request.PhysicalAddress, request.PhysicalCapacity, request.PhysicalBookingDeadline, intake);
        ValidateCapacity(session, intake.MinEnrollment);
        intake.Sessions.Add(session);
        KeepScheduleStatus(intake);
        await SaveForStaffAsync(intake, staffAccountId, "CourseSessionAdded", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> PublishForStaffAsync(
        int staffAccountId,
        int courseIntakeId,
        Guid version,
        CancellationToken ct = default)
    {
        var intake = await LoadForStaffAsync(staffAccountId, courseIntakeId, ct);
        CourseIntakeValidation.Version(intake, version);
        if (intake.Status == CourseIntakeStatus.Published) return ToDto(intake);
        if (intake.Status is not (CourseIntakeStatus.Draft or CourseIntakeStatus.Rejected))
            throw new CourseIntakeException("INTAKE_NOT_PUBLISHABLE", "Only a draft Intake can be published.", 409);
        await RequirePublishedCourseAsync(intake.CourseId, ct);
        await RequireAcceptedTrainerAsync(intake.CourseId, intake.TrainerId, ct);
        if (intake.Sessions.Count == 0)
            throw new CourseIntakeException("SESSION_REQUIRED", "Add at least one valid session before publishing.", field: "sessions");
        foreach (var session in intake.Sessions)
        {
            CourseIntakeValidation.Session(session, intake.StartsAt, intake.EndsAt);
            ValidateCapacity(session, intake.MinEnrollment);
        }
        var now = DateTime.UtcNow;
        intake.Status = CourseIntakeStatus.Published;
        intake.SubmittedAt = now;
        intake.ConfirmedAt = now;
        intake.ConfirmedByCreatorId = await db.Users.Where(item => item.Email == PlatformIdentity.Email)
            .Select(item => item.Id).SingleAsync(ct);
        await SaveForStaffAsync(intake, staffAccountId, "CourseIntakePublished", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> UpdateAsync(int creatorUserId, int courseIntakeId, UpdateCourseIntakeRequest request, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(creatorUserId, courseIntakeId, request.Version, ct);
        ValidateDates(request.RegistrationOpensAt, request.RegistrationClosesAt, request.StartsAt, request.EndsAt, request.DaysBeforeTeaching);
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
        session.Label = next.Label;
        session.StartsAt = next.StartsAt;
        session.EndsAt = next.EndsAt;
        session.MeetingLink = next.MeetingLink;
        session.PhysicalAddress = next.PhysicalAddress;
        session.PhysicalCapacity = next.PhysicalCapacity;
        session.PhysicalBookingDeadline = next.PhysicalBookingDeadline;
        KeepScheduleStatus(intake);
        await SaveAsync(intake, creatorUserId, "CourseSessionUpdated", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> DeleteSessionAsync(int creatorUserId, int courseIntakeId, int courseSessionId, Guid version, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(creatorUserId, courseIntakeId, version, ct);
        var session = FindSession(intake, courseSessionId);
        await RequireNoHistoryAsync(session.Id, ct);
        db.CourseSessions.Remove(session);
        intake.Sessions.Remove(session);
        KeepScheduleStatus(intake);
        await SaveAsync(intake, creatorUserId, "CourseSessionDeleted", ct);
        return ToDto(intake);
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
        CourseIntakeValidation.Dates(intake.RegistrationOpensAt, intake.RegistrationClosesAt, intake.StartsAt, intake.EndsAt);
        if (intake.Sessions.Count == 0)
            throw new CourseIntakeException("SESSION_REQUIRED", "Add at least one valid session before publishing.", field: "sessions");
        foreach (var session in intake.Sessions)
        {
            CourseIntakeValidation.Session(session, intake.StartsAt, intake.EndsAt);
            ValidateCapacity(session, intake.MinEnrollment);
        }
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
                Title = "Class assigned",
                Body = $"You are teaching Intake #{intake.Id} for {intake.Course.Code} {intake.Course.Title}.",
            };
            db.Notifications.Add(notification);
        }
        await SaveWithCurrentVersionAsync(intake, creatorUserId, "CourseIntakePublished", null, ct);
        if (notification is not null && realtime is not null)
            await realtime.PublishAsync(notification, ct);
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

    private async Task RequireOwnedPublishedCourseAsync(int creatorUserId, int courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == courseId && c.CreatorId == creatorUserId, ct)
            ?? throw new CourseIntakeException("COURSE_NOT_FOUND", "Course was not found.", 404);
        if (course.Status != CourseStatus.Published)
            throw new CourseIntakeException("COURSE_NOT_PUBLISHED", "The Course must be published.", 409);
    }

    private async Task RequireAcceptedTrainerAsync(int courseId, int trainerId, CancellationToken ct)
    {
        var accepted = await db.CourseTrainers.AnyAsync(item => item.CourseId == courseId && item.TrainerId == trainerId
            && item.Status == CourseTrainerStatus.Accepted
            && item.Trainer.IsActive && item.Trainer.Roles.Any(role => role.Role == AppRole.Trainer), ct);
        if (!accepted)
            throw new CourseIntakeException("TRAINER_NOT_ACCEPTED", "Choose a Trainer who has accepted this course.", 409, "trainerId");
    }

    private async Task<CourseIntake> LoadOwnedAsync(int userId, int id, CancellationToken ct)
    {
        await RequireTrainerAsync(userId, ct);
        return await db.CourseIntakes.Include(i => i.Sessions).ThenInclude(session => session.Enrollments)
            .Include(i => i.Applications).Include(i => i.ReplacementIntake).AsSplitQuery()
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

    private async Task<StaffAccount> RequireStaffAccountAsync(int staffAccountId, CancellationToken ct)
        => await db.StaffAccounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == staffAccountId && item.IsActive, ct)
            ?? throw new CourseIntakeException("STAFF_REQUIRED", "An active staff account is required.", 403);

    private async Task<CourseIntake> LoadForStaffAsync(int staffAccountId, int id, CancellationToken ct)
    {
        var staff = await RequireStaffAccountAsync(staffAccountId, ct);
        return await db.CourseIntakes.Include(item => item.Sessions).ThenInclude(session => session.Enrollments)
            .Include(item => item.Applications).Include(item => item.ReplacementIntake).AsSplitQuery()
            .Include(item => item.Course).Include(item => item.Trainer)
            .SingleOrDefaultAsync(item => item.Id == id && item.TrainerId == staff.UserId
                && item.Course.IsPlatformOwned, ct)
            ?? throw new CourseIntakeException("INTAKE_NOT_FOUND", "Owned Intake was not found.", 404);
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
        CourseIntakeValidation.Session(session, intake.StartsAt, intake.EndsAt);
        return session;
    }


    private static void ValidateDates(DateTime opens, DateTime closes, DateTime starts, DateTime ends, int daysBeforeTeaching = 0)
    {
        CourseIntakeValidation.Utc(opens, "registrationOpensAt");
        CourseIntakeValidation.Utc(closes, "registrationClosesAt");
        CourseIntakeValidation.Utc(starts, "startsAt");
        CourseIntakeValidation.Utc(ends, "endsAt");
        CourseIntakeValidation.Dates(opens, closes, starts, ends, daysBeforeTeaching);
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

    private async Task SaveForStaffAsync(
        CourseIntake intake,
        int staffAccountId,
        string action,
        CancellationToken ct)
    {
        intake.Version = Guid.NewGuid();
        db.AuditLogs.Add(new AuditLog
        {
            StaffAccountId = staffAccountId,
            Action = action,
            EntityType = nameof(CourseIntake),
            EntityId = intake.Id.ToString(),
            Result = intake.Status.ToString(),
        });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException)
        {
            throw new CourseIntakeException("INTAKE_VERSION_CONFLICT", "The Intake changed. Refresh and retry.", 409);
        }
    }

    private static CourseIntakeDetailDto ToDto(CourseIntake i)
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
            reserved, active, remaining);
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
