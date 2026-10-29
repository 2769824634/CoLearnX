using System.Text.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed partial class CourseIntakeService
{
    public async Task<IReadOnlyList<CourseIntakeDetailDto>> ListSchedulesForAdminAsync(
        int adminAccountId, int courseId, CancellationToken ct = default)
    {
        await RequireSchedulingAdminAsync(adminAccountId, ct);
        if (!await db.Courses.AnyAsync(item => item.Id == courseId, ct))
            throw new CourseIntakeException("COURSE_NOT_FOUND", "Course was not found.", 404);
        var items = await ScheduleQuery().AsNoTracking().Where(item => item.CourseId == courseId)
            .OrderByDescending(item => item.StartsAt).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public Task<CourseIntakeDetailDto> UpdateTeachingDatesForAdminAsync(
        int adminAccountId, int courseId, int intakeId, UpdateTeachingDatesRequest request, CancellationToken ct = default)
        => SaveTeachingDatesAsync(adminAccountId, null, courseId, intakeId, request, ct);

    public Task<CourseIntakeDetailDto> UpdateTeachingDatesForStaffAsync(
        int staffAccountId, int intakeId, UpdateTeachingDatesRequest request, CancellationToken ct = default)
        => SaveTeachingDatesAsync(null, staffAccountId, null, intakeId, request, ct);

    private IQueryable<CourseIntake> ScheduleQuery() => db.CourseIntakes
        .Include(item => item.Sessions).ThenInclude(session => session.Enrollments)
        .Include(item => item.Applications).Include(item => item.ReplacementIntake).AsSplitQuery();

    private async Task RequireSchedulingAdminAsync(int adminAccountId, CancellationToken ct)
    {
        if (!await db.AdminAccounts.AnyAsync(item => item.Id == adminAccountId && item.IsActive, ct))
            throw new CourseIntakeException("ADMIN_REQUIRED", "An active administrator account is required.", 403);
    }

    private async Task<CourseIntakeDetailDto> SaveTeachingDatesAsync(
        int? adminAccountId, int? staffAccountId, int? courseId, int intakeId, UpdateTeachingDatesRequest request, CancellationToken ct)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            CourseIntake intake;
            if (adminAccountId.HasValue)
            {
                await RequireSchedulingAdminAsync(adminAccountId.Value, ct);
                intake = await ScheduleQuery().SingleOrDefaultAsync(item => item.Id == intakeId && item.CourseId == courseId, ct)
                    ?? throw new CourseIntakeException("INTAKE_NOT_FOUND", "Class was not found in this course.", 404);
            }
            else
            {
                var staff = await RequireStaffAccountAsync(staffAccountId!.Value, ct);
                if (!await db.Users.AnyAsync(user => user.Id == staff.UserId && user.IsActive, ct))
                    throw new CourseIntakeException("STAFF_REQUIRED", "An active staff account is required.", 403);
                intake = await LoadForStaffAsync(staff.Id, intakeId, ct);
            }
            CourseIntakeValidation.Version(intake, request.Version);
            if (intake.Status is CourseIntakeStatus.Completed or CourseIntakeStatus.Cancelled or CourseIntakeStatus.PendingApproval)
                throw new CourseIntakeException("INTAKE_NOT_EDITABLE", "Teaching dates cannot change for a completed, cancelled or pending class.", 409);
            CourseIntakeValidation.Utc(request.StartsAt, "startsAt");
            CourseIntakeValidation.Utc(request.EndsAt, "endsAt");
            if (request.StartsAt >= request.EndsAt || request.StartsAt < intake.RegistrationClosesAt)
                throw new CourseIntakeException("INVALID_INTAKE_DATES", "Teaching must start on or after registration closes and end after it starts.", field: "endsAt");
            var now = DateTime.UtcNow;
            if (request.StartsAt != intake.StartsAt && (intake.StartsAt <= now || request.StartsAt <= now))
                throw new CourseIntakeException("TEACHING_ALREADY_STARTED", "The first teaching date cannot change after teaching starts or move into the past.", 409, "startsAt");
            if (request.EndsAt != intake.EndsAt && request.EndsAt <= now)
                throw new CourseIntakeException("INVALID_INTAKE_DATES", "The last teaching date must be in the future.", field: "endsAt");
            foreach (var session in intake.Sessions)
                CourseIntakeValidation.Session(session, request.StartsAt, request.EndsAt);
            var before = new { intake.StartsAt, intake.EndsAt };
            intake.StartsAt = request.StartsAt;
            intake.EndsAt = request.EndsAt;
            intake.Version = Guid.NewGuid();
            db.AuditLogs.Add(new AuditLog
            {
                AdminAccountId = adminAccountId, StaffAccountId = staffAccountId,
                Action = "TeachingDatesUpdated", EntityType = nameof(CourseIntake), EntityId = intake.Id.ToString(),
                Result = intake.Status.ToString(),
                Reason = JsonSerializer.Serialize(new { before, after = new { intake.StartsAt, intake.EndsAt } },
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                throw new CourseIntakeException("INTAKE_VERSION_CONFLICT", "The class changed. Reload it before trying again.", 409);
            }
            await transaction.CommitAsync(ct);
            return ToDto(intake);
        });
    }
}
