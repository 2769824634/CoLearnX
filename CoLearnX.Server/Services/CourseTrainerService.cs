using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ICourseTrainerService
{
    Task<IReadOnlyList<CourseTrainerDto>> ListAsync(int creatorId, int courseId, CancellationToken ct = default);
    Task<IReadOnlyList<TrainerCandidateDto>> SearchCandidatesAsync(int creatorId, int courseId, string? search, CancellationToken ct = default);
    Task<CourseTrainerDto> InviteAsync(int creatorId, int courseId, int trainerId, CancellationToken ct = default);
    Task<CourseTrainerDto> RemoveAsync(int creatorId, int courseId, int trainerId, CancellationToken ct = default);
    Task<IReadOnlyList<CourseInvitationDto>> ListInvitationsAsync(int trainerId, CancellationToken ct = default);
    Task<CourseInvitationDto> RespondAsync(int trainerId, int invitationId, bool accept, CancellationToken ct = default);
    Task<IReadOnlyList<TrainerCourseApplicationDto>> ListAvailableAsync(int trainerId, CancellationToken ct = default);
    Task<CourseInvitationDto> ApplyAsync(int trainerId, int courseId, CancellationToken ct = default);
    Task<CourseTrainerDto> ReviewRequestAsync(int creatorId, int courseId, int trainerId, bool accept, CancellationToken ct = default);
}

public sealed class CourseTrainerService(CoLearnXDbContext db, INotificationRealtime? realtime = null) : ICourseTrainerService
{
    private const int CandidateLimit = 20;

    public async Task<IReadOnlyList<CourseTrainerDto>> ListAsync(int creatorId, int courseId, CancellationToken ct = default)
    {
        await OwnedCourseAsync(creatorId, courseId, ct);
        var rows = await db.CourseTrainers.AsNoTracking()
            .Include(row => row.Trainer).ThenInclude(trainer => trainer.TrainerProfile)
            .Where(row => row.CourseId == courseId)
            .ToListAsync(ct);
        return rows
            .OrderBy(row => RosterOrder(row.Status))
            .ThenBy(row => row.Trainer.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)
            .ToList();
    }

    public async Task<IReadOnlyList<TrainerCandidateDto>> SearchCandidatesAsync(int creatorId, int courseId, string? search, CancellationToken ct = default)
    {
        var subjectId = (await OwnedCourseAsync(creatorId, courseId, ct)).CourseSubjectId;
        var query = ActiveTrainers().AsNoTracking()
            .Where(user => user.Id != creatorId
                && subjectId != null && user.Specialties.Any(item => item.CourseSubjectId == subjectId));
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = $"%{term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            query = query.Where(user => EF.Functions.Like(user.FullName, pattern, "\\")
                || EF.Functions.Like(user.DisplayName, pattern, "\\"));
        }

        return await query
            .OrderBy(user => user.FullName)
            .Take(CandidateLimit)
            .Select(user => new TrainerCandidateDto(user.Id, user.FullName,
                user.TrainerProfile == null ? null : user.TrainerProfile.Headline,
                user.TrainerProfile == null ? null : user.TrainerProfile.Specialisations,
                user.Specialties.OrderBy(item => item.CourseSubject.Name).Select(item => item.CourseSubject.Name).ToList()))
            .ToListAsync(ct);
    }

    public async Task<CourseTrainerDto> InviteAsync(int creatorId, int courseId, int trainerId, CancellationToken ct = default)
    {
        var course = await OwnedCourseAsync(creatorId, courseId, ct);
        if (trainerId == creatorId)
            throw new CourseException("CREATOR_CANNOT_TEACH", "A Creator cannot teach. Invite a Trainer account instead.", StatusCodes.Status400BadRequest);
        var trainer = await ActiveTrainers().Include(user => user.TrainerProfile)
            .SingleOrDefaultAsync(user => user.Id == trainerId, ct)
            ?? throw new CourseException("TRAINER_NOT_FOUND", "This Trainer was not found or cannot teach right now.", StatusCodes.Status404NotFound);
        if (course.CourseSubjectId is null
            || !await db.UserSpecialties.AnyAsync(item => item.UserId == trainerId && item.CourseSubjectId == course.CourseSubjectId, ct))
            throw new CourseException("TRAINER_SUBJECT_MISMATCH", "This Trainer is not certified in the specialty of this Course.", StatusCodes.Status409Conflict);
        var row = await db.CourseTrainers.SingleOrDefaultAsync(item => item.CourseId == courseId && item.TrainerId == trainerId, ct);
        if (row is { Status: CourseTrainerStatus.Invited or CourseTrainerStatus.Accepted })
            throw new CourseException("TRAINER_ALREADY_INVITED", "This Trainer is already invited to or teaching this Course.", StatusCodes.Status409Conflict);

        if (row is null)
        {
            row = new CourseTrainer { CourseId = courseId, TrainerId = trainerId };
            db.CourseTrainers.Add(row);
        }

        row.Status = CourseTrainerStatus.Invited;
        row.InvitedByUserId = creatorId;
        row.InvitedAt = DateTime.UtcNow;
        row.RespondedAt = null;
        row.RemovedAt = null;
        row.Trainer = trainer;

        var notification = new Notification
        {
            UserId = trainerId, Code = "N-course-invitation", Title = "Teaching invitation", EmailPending = true,
            Body = $"{course.Creator.FullName} invited you to teach {CourseName(course)}.",
        };
        db.Notifications.Add(notification);

        await Audit(creatorId, "CourseTrainerInvited", row, ct);
        await db.SaveChangesAsync(ct);
        await PublishAsync(notification, ct);
        return ToDto(row);
    }

    public async Task<CourseTrainerDto> RemoveAsync(int creatorId, int courseId, int trainerId, CancellationToken ct = default)
    {
        var course = await OwnedCourseAsync(creatorId, courseId, ct);
        var row = await db.CourseTrainers
            .Include(item => item.Trainer).ThenInclude(trainer => trainer.TrainerProfile)
            .SingleOrDefaultAsync(item => item.CourseId == courseId && item.TrainerId == trainerId, ct)
            ?? throw new CourseException("COURSE_TRAINER_NOT_FOUND", "This Trainer is not on the Course roster.", StatusCodes.Status404NotFound);
        if (row.Status is CourseTrainerStatus.Removed or CourseTrainerStatus.Declined)
            return ToDto(row);

        var withdrawn = row.Status == CourseTrainerStatus.Invited;
        row.Status = CourseTrainerStatus.Removed;
        row.RemovedAt = DateTime.UtcNow;
        Notification? notification = null;
        if (trainerId != creatorId)
        {
            notification = new Notification
            {
                UserId = trainerId, Code = "N-course-trainer-removed",
                Title = withdrawn ? "Invitation withdrawn" : "Removed from a Course",
                Body = withdrawn
                    ? $"{course.Creator.FullName} withdrew the invitation to teach {CourseName(course)}."
                    : $"{course.Creator.FullName} removed you from the Trainers for {CourseName(course)}.",
            };
            db.Notifications.Add(notification);
        }

        await Audit(creatorId, "CourseTrainerRemoved", row, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<IReadOnlyList<CourseInvitationDto>> ListInvitationsAsync(int trainerId, CancellationToken ct = default)
    {
        var rows = await db.CourseTrainers.AsNoTracking()
            .Include(row => row.Course).ThenInclude(course => course.Creator)
            .Where(row => row.TrainerId == trainerId)
            .ToListAsync(ct);
        return rows
            .OrderBy(row => row.Status == CourseTrainerStatus.Invited ? 0 : 1)
            .ThenByDescending(row => row.InvitedAt)
            .Select(ToInvitationDto)
            .ToList();
    }

    public async Task<CourseInvitationDto> RespondAsync(int trainerId, int invitationId, bool accept, CancellationToken ct = default)
    {
        var row = await db.CourseTrainers
            .Include(item => item.Course).ThenInclude(course => course.Creator)
            .Include(item => item.Trainer)
            .SingleOrDefaultAsync(item => item.Id == invitationId && item.TrainerId == trainerId, ct)
            ?? throw new CourseException("INVITATION_NOT_FOUND", "This invitation was not found.", StatusCodes.Status404NotFound);
        var target = accept ? CourseTrainerStatus.Accepted : CourseTrainerStatus.Declined;
        if (row.Status == target)
            return ToInvitationDto(row);
        if (row.Status != CourseTrainerStatus.Invited)
            throw new CourseException("INVITATION_NOT_PENDING", "This invitation is no longer waiting for your answer.", StatusCodes.Status409Conflict);

        row.Status = target;
        row.RespondedAt = DateTime.UtcNow;
        var notification = new Notification
        {
            UserId = row.Course.CreatorId,
            Code = accept ? "N-course-invitation-accepted" : "N-course-invitation-declined",
            Title = accept ? "Invitation accepted" : "Invitation declined",
            Body = $"{row.Trainer.FullName} {(accept ? "accepted" : "declined")} your invitation to teach {CourseName(row.Course)}.",
        };
        db.Notifications.Add(notification);
        await Audit(trainerId, accept ? "CourseInvitationAccepted" : "CourseInvitationDeclined", row, ct);
        await db.SaveChangesAsync(ct);
        await PublishAsync(notification, ct);
        return ToInvitationDto(row);
    }

    public async Task<IReadOnlyList<TrainerCourseApplicationDto>> ListAvailableAsync(int trainerId, CancellationToken ct = default)
    {
        await RequireActiveTrainerAsync(trainerId, ct);
        var courses = await db.Courses.AsNoTracking()
            .Where(course => course.Status == CourseStatus.Published
                && course.OpenToTrainerApplications
                && course.CourseSubjectId != null
                && db.UserSpecialties.Any(specialty => specialty.UserId == trainerId && specialty.CourseSubjectId == course.CourseSubjectId))
            .OrderBy(course => course.Title)
            .Select(course => new
            {
                course.Id, course.Code, course.Title, CreatorName = course.Creator.FullName,
                SpecialtyName = course.CourseSubject!.Name,
                Status =
                db.CourseTrainers.Where(row => row.CourseId == course.Id && row.TrainerId == trainerId)
                    .Select(row => (CourseTrainerStatus?)row.Status).SingleOrDefault(),
            })
            .ToListAsync(ct);
        return courses.Select(course => new TrainerCourseApplicationDto(
            course.Id, course.Code, course.Title, course.CreatorName, course.SpecialtyName, course.Status?.ToString()))
            .ToList();
    }

    public async Task<CourseInvitationDto> ApplyAsync(int trainerId, int courseId, CancellationToken ct = default)
    {
        var trainer = await RequireActiveTrainerAsync(trainerId, ct);
        var course = await db.Courses.Include(item => item.Creator).Include(item => item.CourseSubject)
            .SingleOrDefaultAsync(item => item.Id == courseId, ct)
            ?? throw new CourseException("COURSE_NOT_FOUND", "Course was not found.", StatusCodes.Status404NotFound);
        if (course.Status != CourseStatus.Published)
            throw new CourseException("COURSE_NOT_PUBLISHED", "Only published Courses accept teaching applications.", StatusCodes.Status409Conflict);
        if (!course.OpenToTrainerApplications)
            throw new CourseException("COURSE_NOT_OPEN_TO_APPLICATIONS", "This Course is by invitation only.", StatusCodes.Status409Conflict);
        if (course.CourseSubjectId is null
            || !await db.UserSpecialties.AnyAsync(item => item.UserId == trainerId && item.CourseSubjectId == course.CourseSubjectId, ct))
            throw new CourseException("TRAINER_SUBJECT_MISMATCH", "You can only apply for Courses in your certified specialties.", StatusCodes.Status409Conflict);

        var row = await db.CourseTrainers.SingleOrDefaultAsync(item => item.CourseId == courseId && item.TrainerId == trainerId, ct);
        if (row is { Status: CourseTrainerStatus.Requested or CourseTrainerStatus.Invited or CourseTrainerStatus.Accepted })
            throw new CourseException("APPLICATION_ALREADY_EXISTS", "You already have an active invitation or application for this Course.", StatusCodes.Status409Conflict);
        if (row is null)
        {
            row = new CourseTrainer { CourseId = courseId, TrainerId = trainerId };
            db.CourseTrainers.Add(row);
        }
        row.Status = CourseTrainerStatus.Requested;
        row.InvitedByUserId = trainerId;
        row.InvitedAt = DateTime.UtcNow;
        row.RespondedAt = null;
        row.RemovedAt = null;
        row.Course = course;
        row.Trainer = trainer;

        var notification = new Notification
        {
            UserId = course.CreatorId, Code = "N-course-trainer-requested", Title = "Teaching application",
            Body = $"{trainer.FullName} applied to teach {CourseName(course)}.", EmailPending = true,
        };
        db.Notifications.Add(notification);
        await Audit(trainerId, "CourseTrainerRequested", row, ct);
        await db.SaveChangesAsync(ct);
        await PublishAsync(notification, ct);
        return ToInvitationDto(row);
    }

    public async Task<CourseTrainerDto> ReviewRequestAsync(int creatorId, int courseId, int trainerId, bool accept, CancellationToken ct = default)
    {
        var course = await OwnedCourseAsync(creatorId, courseId, ct);
        var row = await db.CourseTrainers.Include(item => item.Trainer).ThenInclude(user => user.TrainerProfile)
            .SingleOrDefaultAsync(item => item.CourseId == courseId && item.TrainerId == trainerId, ct)
            ?? throw new CourseException("APPLICATION_NOT_FOUND", "This teaching application was not found.", StatusCodes.Status404NotFound);
        if (row.Status != CourseTrainerStatus.Requested)
            throw new CourseException("APPLICATION_NOT_PENDING", "This teaching application is no longer waiting for review.", StatusCodes.Status409Conflict);
        if (accept && (!row.Trainer.IsActive
            || !await db.UserRoles.AnyAsync(role => role.UserId == trainerId && role.Role == AppRole.Trainer && role.CertifiedAt != null, ct)
            || course.CourseSubjectId is null
            || !await db.UserSpecialties.AnyAsync(item => item.UserId == trainerId && item.CourseSubjectId == course.CourseSubjectId, ct)))
            throw new CourseException("TRAINER_NOT_ELIGIBLE", "This Trainer is no longer certified for the Course specialty.", StatusCodes.Status409Conflict);
        row.Status = accept ? CourseTrainerStatus.Accepted : CourseTrainerStatus.Declined;
        row.RespondedAt = DateTime.UtcNow;
        var notification = new Notification
        {
            UserId = trainerId,
            Code = accept ? "N-course-application-accepted" : "N-course-application-declined",
            Title = accept ? "Teaching application accepted" : "Teaching application declined",
            Body = $"Your application to teach {CourseName(course)} was {(accept ? "accepted" : "declined")}.",
        };
        db.Notifications.Add(notification);
        await Audit(creatorId, accept ? "CourseTrainerRequestAccepted" : "CourseTrainerRequestDeclined", row, ct);
        await db.SaveChangesAsync(ct);
        await PublishAsync(notification, ct);
        return ToDto(row);
    }

    // A first invitation has no row Id until saved; the course/trainer pair keeps the entry in the same save.
    private async Task Audit(int actorUserId, string action, CourseTrainer row, CancellationToken ct)
    {
        var log = await TrainerAudit.CreateAsync(
            db, actorUserId, action, nameof(CourseTrainer), row.CourseId, row.Status.ToString(), null, ct);
        log.EntityId = $"{row.CourseId}/{row.TrainerId}";
        db.AuditLogs.Add(log);
    }

    private IQueryable<User> ActiveTrainers()
        => db.Users.Where(user => user.IsActive
            && user.Roles.Any(role => role.Role == AppRole.Trainer && role.CertifiedAt != null));

    private async Task<User> RequireActiveTrainerAsync(int trainerId, CancellationToken ct)
        => await ActiveTrainers().SingleOrDefaultAsync(user => user.Id == trainerId, ct)
            ?? throw new CourseException("TRAINER_NOT_FOUND", "This Trainer was not found or cannot teach right now.", StatusCodes.Status404NotFound);

    private async Task<Course> OwnedCourseAsync(int creatorId, int courseId, CancellationToken ct)
        => await db.Courses.Include(course => course.Creator).Include(course => course.CourseSubject)
            .SingleOrDefaultAsync(course => course.Id == courseId && course.CreatorId == creatorId, ct)
            ?? throw new CourseException("COURSE_NOT_FOUND", "Course was not found.", StatusCodes.Status404NotFound);

    private async Task PublishAsync(Notification? notification, CancellationToken ct)
    {
        if (notification is not null && realtime is not null)
            await realtime.PublishAsync(notification, ct);
    }

    private static string CourseName(Course course) => $"{course.Code} {course.Title}";

    private static int RosterOrder(CourseTrainerStatus status) => status switch
    {
        CourseTrainerStatus.Accepted => 0,
        CourseTrainerStatus.Requested => 1,
        CourseTrainerStatus.Invited => 2,
        CourseTrainerStatus.Declined => 3,
        _ => 4,
    };

    private static CourseTrainerDto ToDto(CourseTrainer row) => new(
        row.TrainerId, row.Trainer.FullName, row.Trainer.TrainerProfile?.Headline, row.Status.ToString(),
        row.InvitedAt, row.RespondedAt, row.RemovedAt);

    private static CourseInvitationDto ToInvitationDto(CourseTrainer row) => new(
        row.Id, row.CourseId, row.Course.Code, row.Course.Title, row.Course.Creator.FullName, row.Status.ToString(),
        row.InvitedAt, row.RespondedAt, row.RemovedAt);
}
