using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
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
    Task<CourseIntakeDetailDto> CreateAsync(int trainerUserId, int courseId, CreateCourseIntakeRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> UpdateAsync(int trainerUserId, int courseIntakeId, UpdateCourseIntakeRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> AddSessionAsync(int trainerUserId, int courseIntakeId, CreateCourseSessionRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> UpdateSessionAsync(int trainerUserId, int courseIntakeId, int courseSessionId, UpdateCourseSessionRequest request, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> DeleteSessionAsync(int trainerUserId, int courseIntakeId, int courseSessionId, Guid version, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> SubmitAsync(int trainerUserId, int courseIntakeId, Guid version, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> RequestChangeAsync(int trainerUserId, int courseIntakeId, CreateCourseIntakeChangeRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CreatorIntakeApplicationSummaryDto>> ListCreatorApplicationsAsync(int creatorUserId, CancellationToken ct = default);
    Task<CreatorIntakeApplicationDetailDto> GetCreatorApplicationAsync(int creatorUserId, int courseIntakeId, CancellationToken ct = default);
    Task<CourseIntakeDetailDto> ReviewAsync(int creatorUserId, int courseIntakeId, ReviewIntakeApplicationRequest request, CancellationToken ct = default);
}

public sealed class CourseIntakeService(CoLearnXDbContext db) : ICourseIntakeService
{
    public async Task<IReadOnlyList<CourseIntakeSummaryDto>> ListOwnedAsync(int trainerUserId, CancellationToken ct = default)
    {
        await RequireTrainerAsync(trainerUserId, ct);
        return await db.CourseIntakes.AsNoTracking().Where(i => i.TrainerId == trainerUserId).OrderByDescending(i => i.Id)
            .Select(i => new CourseIntakeSummaryDto(i.Id, i.CourseId, i.TrainerId, i.StartsAt, i.EndsAt, i.Status.ToString(), i.Version)).ToListAsync(ct);
    }

    public async Task<CourseIntakeDetailDto> GetOwnedAsync(int trainerUserId, int courseIntakeId, CancellationToken ct = default)
        => ToDto(await LoadOwnedAsync(trainerUserId, courseIntakeId, ct));

    public async Task<CourseIntakeDetailDto> CreateAsync(int trainerUserId, int courseId, CreateCourseIntakeRequest request, CancellationToken ct = default)
    {
        await RequireTrainerAsync(trainerUserId, ct);
        await RequirePublishedCourseAsync(courseId, ct);
        ValidateDates(request.RegistrationOpensAt, request.RegistrationClosesAt, request.StartsAt, request.EndsAt);
        var intake = new CourseIntake { CourseId = courseId, TrainerId = trainerUserId,
            RegistrationOpensAt = request.RegistrationOpensAt, RegistrationClosesAt = request.RegistrationClosesAt,
            StartsAt = request.StartsAt, EndsAt = request.EndsAt };
        // A zero-session Draft is allowed; submission enforces the diagram's 1..* structure.
        db.CourseIntakes.Add(intake);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct); // obtain the generated ID before writing its audit record
        await SaveAsync(intake, "CourseIntakeCreated", ct);
        await transaction.CommitAsync(ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> UpdateAsync(int trainerUserId, int courseIntakeId, UpdateCourseIntakeRequest request, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(trainerUserId, courseIntakeId, request.Version, ct);
        ValidateDates(request.RegistrationOpensAt, request.RegistrationClosesAt, request.StartsAt, request.EndsAt);
        foreach (var session in intake.Sessions) CourseIntakeValidation.Session(session, request.StartsAt, request.EndsAt);
        intake.RegistrationOpensAt = request.RegistrationOpensAt;
        intake.RegistrationClosesAt = request.RegistrationClosesAt;
        intake.StartsAt = request.StartsAt;
        intake.EndsAt = request.EndsAt;
        ResetDraft(intake);
        await SaveAsync(intake, "CourseIntakeUpdated", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> AddSessionAsync(int trainerUserId, int courseIntakeId, CreateCourseSessionRequest request, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(trainerUserId, courseIntakeId, request.Version, ct);
        var session = BuildSession(request.Label, request.StartsAt, request.EndsAt, request.MeetingLink,
            request.PhysicalAddress, request.PhysicalCapacity, request.PhysicalBookingDeadline, intake);
        intake.Sessions.Add(session);
        ResetDraft(intake);
        await SaveAsync(intake, "CourseSessionAdded", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> UpdateSessionAsync(int trainerUserId, int courseIntakeId, int courseSessionId, UpdateCourseSessionRequest request, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(trainerUserId, courseIntakeId, request.Version, ct);
        var session = FindSession(intake, courseSessionId);
        var next = BuildSession(request.Label, request.StartsAt, request.EndsAt, request.MeetingLink,
            request.PhysicalAddress, request.PhysicalCapacity, request.PhysicalBookingDeadline, intake);
        await RequireNoHistoryAsync(session.Id, ct);
        session.Label = next.Label;
        session.StartsAt = next.StartsAt;
        session.EndsAt = next.EndsAt;
        session.MeetingLink = next.MeetingLink;
        session.PhysicalAddress = next.PhysicalAddress;
        session.PhysicalCapacity = next.PhysicalCapacity;
        session.PhysicalBookingDeadline = next.PhysicalBookingDeadline;
        ResetDraft(intake);
        await SaveAsync(intake, "CourseSessionUpdated", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> DeleteSessionAsync(int trainerUserId, int courseIntakeId, int courseSessionId, Guid version, CancellationToken ct = default)
    {
        var intake = await LoadEditableAsync(trainerUserId, courseIntakeId, version, ct);
        var session = FindSession(intake, courseSessionId);
        await RequireNoHistoryAsync(session.Id, ct);
        db.CourseSessions.Remove(session);
        intake.Sessions.Remove(session);
        ResetDraft(intake);
        await SaveAsync(intake, "CourseSessionDeleted", ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> SubmitAsync(int trainerUserId, int courseIntakeId, Guid version, CancellationToken ct = default)
    {
        var intake = await LoadOwnedAsync(trainerUserId, courseIntakeId, ct);
        CourseIntakeValidation.Version(intake, version);
        if (intake.Status == CourseIntakeStatus.PendingApproval) return ToDto(intake);
        CourseIntakeValidation.Editable(intake);
        await RequirePublishedCourseAsync(intake.CourseId, ct);
        CourseIntakeValidation.Dates(intake.RegistrationOpensAt, intake.RegistrationClosesAt, intake.StartsAt, intake.EndsAt);
        if (intake.Sessions.Count == 0)
            throw new CourseIntakeException("SESSION_REQUIRED", "Add at least one valid session before submission.", field: "sessions");
        foreach (var session in intake.Sessions) CourseIntakeValidation.Session(session, intake.StartsAt, intake.EndsAt);
        intake.Status = CourseIntakeStatus.PendingApproval;
        intake.SubmittedAt = DateTime.UtcNow;
        intake.ConfirmedByCreatorId = null;
        intake.ConfirmedAt = null;
        intake.ConfirmationNote = null;
        var applicationVersion = Guid.NewGuid();
        intake.Version = applicationVersion;
        intake.Applications.Add(new CourseIntakeApplication
        {
            ApplicationVersion = applicationVersion,
            Kind = CourseIntakeApplicationKind.Initial,
            SubmittedByTrainerId = trainerUserId,
            SubmittedAt = intake.SubmittedAt.Value,
        });
        await SaveWithCurrentVersionAsync(intake, trainerUserId, "CourseIntakeSubmitted", null, ct);
        return ToDto(intake);
    }

    public async Task<CourseIntakeDetailDto> RequestChangeAsync(int trainerUserId, int courseIntakeId,
        CreateCourseIntakeChangeRequest request, CancellationToken ct = default)
    {
        var intake = await LoadOwnedAsync(trainerUserId, courseIntakeId, ct);
        CourseIntakeValidation.Version(intake, request.Version);
        var latest = intake.Applications.OrderByDescending(a => a.Id).FirstOrDefault();
        CourseIntakeStatus restoreStatus;
        if (intake.Status is CourseIntakeStatus.Published or CourseIntakeStatus.InProgress)
            restoreStatus = intake.Status;
        else if (intake.Status == CourseIntakeStatus.Rejected
                 && latest is { Kind: CourseIntakeApplicationKind.Change, Status: CourseIntakeApplicationStatus.Rejected, RestoreStatus: not null })
            restoreStatus = latest.RestoreStatus.Value;
        else
            throw new CourseIntakeException("CHANGE_REQUEST_NOT_ALLOWED", "A structural change request requires a Published, In Progress, or previously rejected changed Intake.", 409);

        ValidateDates(request.RegistrationOpensAt, request.RegistrationClosesAt, request.StartsAt, request.EndsAt);
        if (request.Sessions.Count == 0)
            throw new CourseIntakeException("SESSION_REQUIRED", "A change request must contain at least one valid session.", field: "sessions");
        var proposalSessions = BuildProposalSessions(request, intake);
        await RequireHistorySafeProposalAsync(intake, proposalSessions, ct);
        if (!HasMaterialChange(intake, request, proposalSessions))
            throw new CourseIntakeException("NO_MATERIAL_CHANGE", "Use the delivery-link action when only a meeting link changes.", 409);

        var applicationVersion = Guid.NewGuid();
        var submittedAt = DateTime.UtcNow;
        var snapshot = new ChangeSnapshot(request.RegistrationOpensAt, request.RegistrationClosesAt,
            request.StartsAt, request.EndsAt, proposalSessions.Select(ToSnapshot).ToList());
        intake.Status = CourseIntakeStatus.PendingApproval;
        intake.SubmittedAt = submittedAt;
        intake.Version = applicationVersion;
        intake.Applications.Add(new CourseIntakeApplication
        {
            ApplicationVersion = applicationVersion,
            Kind = CourseIntakeApplicationKind.Change,
            RestoreStatus = restoreStatus,
            ProposalJson = JsonSerializer.Serialize(snapshot),
            SubmittedByTrainerId = trainerUserId,
            SubmittedAt = submittedAt,
        });
        await SaveWithCurrentVersionAsync(intake, trainerUserId, "CourseIntakeChangeRequested", null, ct);
        return ToDto(intake);
    }

    public async Task<IReadOnlyList<CreatorIntakeApplicationSummaryDto>> ListCreatorApplicationsAsync(int creatorUserId, CancellationToken ct = default)
    {
        await RequireCreatorAsync(creatorUserId, ct);
        var applications = await db.CourseIntakeApplications.AsNoTracking()
            .Where(a => a.CourseIntake.Course.CreatorId == creatorUserId
                && !db.CourseIntakeApplications.Any(newer => newer.CourseIntakeId == a.CourseIntakeId && newer.Id > a.Id))
            .Include(a => a.CourseIntake).ThenInclude(i => i.Course)
            .Include(a => a.CourseIntake).ThenInclude(i => i.Trainer)
            .OrderBy(a => a.Status).ThenByDescending(a => a.SubmittedAt).ToListAsync(ct);
        return applications.Select(ToCreatorSummary).ToList();
    }

    public async Task<CreatorIntakeApplicationDetailDto> GetCreatorApplicationAsync(int creatorUserId, int courseIntakeId, CancellationToken ct = default)
    {
        var intake = await LoadForCreatorAsync(creatorUserId, courseIntakeId, ct);
        var application = intake.Applications.OrderByDescending(a => a.Id).FirstOrDefault()
            ?? throw new CourseIntakeException("INTAKE_APPLICATION_NOT_FOUND", "The Intake application was not found.", 404);
        return new CreatorIntakeApplicationDetailDto(ToCreatorSummary(application), ToDto(intake),
            application.Kind == CourseIntakeApplicationKind.Change ? ToChangeDto(application) : null);
    }

    public async Task<CourseIntakeDetailDto> ReviewAsync(int creatorUserId, int courseIntakeId,
        ReviewIntakeApplicationRequest request, CancellationToken ct = default)
    {
        var intake = await LoadForCreatorAsync(creatorUserId, courseIntakeId, ct);
        if (intake.TrainerId == creatorUserId)
            throw new CourseIntakeException("CREATOR_SELF_REVIEW_FORBIDDEN", "A Creator cannot review an Intake they submitted as Trainer.", 403);
        var application = intake.Applications.OrderByDescending(a => a.Id).FirstOrDefault()
            ?? throw new CourseIntakeException("INTAKE_APPLICATION_NOT_FOUND", "The Intake application was not found.", 404);
        if (application.ApplicationVersion != request.Version)
            throw new CourseIntakeException("INTAKE_VERSION_CONFLICT", "The Intake application changed. Reload it before reviewing.", 409);

        var decision = ParseDecision(request.Decision);
        var note = NormalizeReviewNote(request.ConfirmationNote);
        if (decision == CourseIntakeApplicationStatus.Rejected && note is null)
            throw new CourseIntakeException("REJECTION_NOTE_REQUIRED", "Explain what the Trainer must change.", field: "confirmationNote");
        if (application.Status != CourseIntakeApplicationStatus.Pending)
        {
            if (application.Status == decision && string.Equals(application.ReviewNote, note, StringComparison.Ordinal))
                return ToDto(intake);
            throw new CourseIntakeException("INTAKE_REVIEW_CONFLICT", "This application already has a different review outcome.", 409);
        }
        CourseIntakeValidation.Version(intake, request.Version);
        if (intake.Status != CourseIntakeStatus.PendingApproval)
            throw new CourseIntakeException("INTAKE_NOT_PENDING", "Only a pending Intake application can be reviewed.", 409);

        if (application.Kind == CourseIntakeApplicationKind.Change && decision == CourseIntakeApplicationStatus.Confirmed)
        {
            var snapshot = ReadSnapshot(application);
            await RequireHistorySafeSnapshotAsync(intake, snapshot, ct);
            ApplySnapshot(intake, snapshot);
            intake.Status = application.RestoreStatus
                ?? throw new CourseIntakeException("INTAKE_SAVE_FAILED", "The change request has no status to restore.", 500);
        }
        else
            intake.Status = decision == CourseIntakeApplicationStatus.Confirmed
                ? CourseIntakeStatus.Published : CourseIntakeStatus.Rejected;

        application.Status = decision;
        application.ReviewedByCreatorId = creatorUserId;
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewNote = note;
        intake.ConfirmedByCreatorId = creatorUserId;
        intake.ConfirmedAt = application.ReviewedAt;
        intake.ConfirmationNote = note;
        intake.Version = Guid.NewGuid();
        await SaveWithCurrentVersionAsync(intake, creatorUserId,
            decision == CourseIntakeApplicationStatus.Confirmed ? "CourseIntakeConfirmed" : "CourseIntakeRejected", note, ct);
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

    private async Task<CourseIntake> LoadOwnedAsync(int userId, int id, CancellationToken ct)
    {
        await RequireTrainerAsync(userId, ct);
        return await db.CourseIntakes.Include(i => i.Sessions).Include(i => i.Applications).AsSplitQuery()
            .SingleOrDefaultAsync(i => i.Id == id && i.TrainerId == userId, ct)
            ?? throw new CourseIntakeException("INTAKE_NOT_FOUND", "Owned Intake was not found.", 404);
    }

    private async Task<CourseIntake> LoadForCreatorAsync(int userId, int id, CancellationToken ct)
    {
        await RequireCreatorAsync(userId, ct);
        return await db.CourseIntakes.Include(i => i.Sessions).Include(i => i.Applications).AsSplitQuery()
            .Include(i => i.Course).Include(i => i.Trainer)
            .SingleOrDefaultAsync(i => i.Id == id && i.Course.CreatorId == userId, ct)
            ?? throw new CourseIntakeException("INTAKE_APPLICATION_NOT_FOUND", "The Intake application was not found.", 404);
    }

    private async Task<CourseIntake> LoadEditableAsync(int userId, int id, Guid version, CancellationToken ct)
    {
        var intake = await LoadOwnedAsync(userId, id, ct);
        CourseIntakeValidation.Version(intake, version);
        CourseIntakeValidation.Editable(intake);
        if (intake.Status == CourseIntakeStatus.Rejected
            && intake.Applications.OrderByDescending(a => a.Id).FirstOrDefault()?.Kind == CourseIntakeApplicationKind.Change)
            throw new CourseIntakeException("CHANGE_REQUEST_REQUIRED", "Revise and resubmit the structural change request instead of editing the live schedule.", 409);
        return intake;
    }

    private async Task RequireNoHistoryAsync(int id, CancellationToken ct)
    {
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

    private static List<CourseSession> BuildProposalSessions(CreateCourseIntakeChangeRequest request, CourseIntake intake)
    {
        var identifiers = request.Sessions.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToList();
        if (identifiers.Any(id => id <= 0) || identifiers.Distinct().Count() != identifiers.Count)
            throw new CourseIntakeException("INVALID_SESSION_REFERENCE", "Each existing Session may appear once and must use a positive ID.", field: "sessions");
        var known = intake.Sessions.Select(s => s.Id).ToHashSet();
        if (identifiers.Any(id => !known.Contains(id)))
            throw new CourseIntakeException("SESSION_NOT_FOUND", "A proposed Session does not belong to this Intake.", 404);

        return request.Sessions.Select(proposed =>
        {
            var session = BuildSession(proposed.Label, proposed.StartsAt, proposed.EndsAt, proposed.MeetingLink,
                proposed.PhysicalAddress, proposed.PhysicalCapacity, proposed.PhysicalBookingDeadline, new CourseIntake
                {
                    Id = intake.Id,
                    StartsAt = request.StartsAt,
                    EndsAt = request.EndsAt,
                });
            session.Id = proposed.Id ?? 0;
            return session;
        }).ToList();
    }

    private async Task RequireHistorySafeProposalAsync(CourseIntake intake, IReadOnlyList<CourseSession> proposed, CancellationToken ct)
    {
        var byId = proposed.Where(s => s.Id > 0).ToDictionary(s => s.Id);
        foreach (var current in intake.Sessions)
        {
            if (!byId.TryGetValue(current.Id, out var next) || !SameStructure(current, next))
                await RequireNoHistoryAsync(current.Id, ct);
        }
    }

    private async Task RequireHistorySafeSnapshotAsync(CourseIntake intake, ChangeSnapshot snapshot, CancellationToken ct)
    {
        var proposed = snapshot.Sessions.Select(FromSnapshot).ToList();
        await RequireHistorySafeProposalAsync(intake, proposed, ct);
    }

    private static bool HasMaterialChange(CourseIntake intake, CreateCourseIntakeChangeRequest request, IReadOnlyList<CourseSession> proposed)
    {
        if (intake.RegistrationOpensAt != request.RegistrationOpensAt || intake.RegistrationClosesAt != request.RegistrationClosesAt
            || intake.StartsAt != request.StartsAt || intake.EndsAt != request.EndsAt || intake.Sessions.Count != proposed.Count)
            return true;
        var byId = proposed.Where(s => s.Id > 0).ToDictionary(s => s.Id);
        return intake.Sessions.Any(current => !byId.TryGetValue(current.Id, out var next) || !SameStructure(current, next));
    }

    private static bool SameStructure(CourseSession first, CourseSession second)
        => first.Label == second.Label && first.StartsAt == second.StartsAt && first.EndsAt == second.EndsAt
            && first.PhysicalAddress == second.PhysicalAddress && first.PhysicalCapacity == second.PhysicalCapacity
            && first.PhysicalBookingDeadline == second.PhysicalBookingDeadline;

    private void ApplySnapshot(CourseIntake intake, ChangeSnapshot snapshot)
    {
        intake.RegistrationOpensAt = snapshot.RegistrationOpensAt;
        intake.RegistrationClosesAt = snapshot.RegistrationClosesAt;
        intake.StartsAt = snapshot.StartsAt;
        intake.EndsAt = snapshot.EndsAt;
        var proposedIds = snapshot.Sessions.Where(s => s.Id.HasValue).Select(s => s.Id!.Value).ToHashSet();
        foreach (var removed in intake.Sessions.Where(s => !proposedIds.Contains(s.Id)).ToList())
        {
            db.CourseSessions.Remove(removed);
            intake.Sessions.Remove(removed);
        }
        foreach (var proposed in snapshot.Sessions)
        {
            var target = proposed.Id.HasValue ? FindSession(intake, proposed.Id.Value) : new CourseSession { CourseIntakeId = intake.Id };
            target.Label = proposed.Label;
            target.StartsAt = proposed.StartsAt;
            target.EndsAt = proposed.EndsAt;
            target.MeetingLink = proposed.MeetingLink;
            target.PhysicalAddress = proposed.PhysicalAddress;
            target.PhysicalCapacity = proposed.PhysicalCapacity;
            target.PhysicalBookingDeadline = proposed.PhysicalBookingDeadline;
            if (!proposed.Id.HasValue) intake.Sessions.Add(target);
        }
    }

    private static void ValidateDates(DateTime opens, DateTime closes, DateTime starts, DateTime ends)
    {
        CourseIntakeValidation.Utc(opens, "registrationOpensAt");
        CourseIntakeValidation.Utc(closes, "registrationClosesAt");
        CourseIntakeValidation.Utc(starts, "startsAt");
        CourseIntakeValidation.Utc(ends, "endsAt");
        CourseIntakeValidation.Dates(opens, closes, starts, ends);
    }

    private static void ResetDraft(CourseIntake intake)
    {
        intake.Status = CourseIntakeStatus.Draft;
        intake.SubmittedAt = null;
        intake.ConfirmedByCreatorId = null;
        intake.ConfirmedAt = null;
        intake.ConfirmationNote = null;
    }

    private async Task SaveAsync(CourseIntake intake, string action, CancellationToken ct)
    {
        intake.Version = Guid.NewGuid();
        await SaveWithCurrentVersionAsync(intake, intake.TrainerId, action, null, ct);
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

    private static CourseIntakeDetailDto ToDto(CourseIntake i)
        => new(i.Id, i.CourseId, i.TrainerId, i.RegistrationOpensAt, i.RegistrationClosesAt, i.StartsAt, i.EndsAt,
            i.Status.ToString(), i.SubmittedAt, i.ConfirmedByCreatorId, i.ConfirmedAt, i.ConfirmationNote, i.Version,
            i.Sessions.OrderBy(s => s.StartsAt).ThenBy(s => s.Id).Select(s => new CourseSessionDto(s.Id, s.Label, s.StartsAt,
                s.EndsAt, s.PhysicalCapacity, s.SeatsLeft, s.CourseIntakeId, s.MeetingLink, s.PhysicalAddress, s.PhysicalCapacity, s.PhysicalBookingDeadline)).ToList(),
            i.Applications.Where(a => a.Kind == CourseIntakeApplicationKind.Change).OrderByDescending(a => a.Id)
                .Select(ToChangeDto).FirstOrDefault());

    private static CreatorIntakeApplicationSummaryDto ToCreatorSummary(CourseIntakeApplication application)
    {
        var intake = application.CourseIntake;
        return new CreatorIntakeApplicationSummaryDto(intake.Id, application.Id, intake.CourseId,
            intake.Course.Code, intake.Course.Title, intake.TrainerId, intake.Trainer.FullName,
            application.Kind.ToString(), application.Status.ToString(), application.SubmittedAt, application.ApplicationVersion);
    }

    private static CourseIntakeChangeDto ToChangeDto(CourseIntakeApplication application)
    {
        var snapshot = ReadSnapshot(application);
        return new CourseIntakeChangeDto(application.Id, application.Status.ToString(), application.RestoreStatus?.ToString() ?? "Published",
            application.SubmittedAt, application.ReviewNote, snapshot.RegistrationOpensAt, snapshot.RegistrationClosesAt,
            snapshot.StartsAt, snapshot.EndsAt, snapshot.Sessions.Select(s => new ProposedCourseSessionRequest(s.Id, s.Label,
                s.StartsAt, s.EndsAt, s.MeetingLink, s.PhysicalAddress, s.PhysicalCapacity, s.PhysicalBookingDeadline)).ToList());
    }

    private static CourseIntakeApplicationStatus ParseDecision(string value)
        => value?.Trim() switch
        {
            "Confirm" => CourseIntakeApplicationStatus.Confirmed,
            "Reject" => CourseIntakeApplicationStatus.Rejected,
            _ => throw new CourseIntakeException("INVALID_REVIEW_DECISION", "Decision must be Confirm or Reject.", field: "decision"),
        };

    private static string? NormalizeReviewNote(string? value)
    {
        var note = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (note?.Length > 512)
            throw new CourseIntakeException("INVALID_CONFIRMATION_NOTE", "Confirmation notes may contain at most 512 characters.", field: "confirmationNote");
        return note;
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
