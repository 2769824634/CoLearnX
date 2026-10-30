using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class IntakeTeachingDatesIntegrationTests(CoLearnXApiFactory factory)
    : IClassFixture<CoLearnXApiFactory>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Admin_and_staff_update_running_dates_without_changing_sessions_or_enrollments(bool asAdmin)
    {
        var setup = await CreateIntakeAsync(CourseIntakeStatus.InProgress);
        using var client = asAdmin ? await ApiClient.AsOperationsAdminAsync(factory) : StaffClient(setup.Staff);
        var path = Path(asAdmin, setup.Intake.Id);
        var original = setup.Intake;
        var startsAt = original.StartsAt.AddDays(-1);
        var endsAt = original.EndsAt.AddDays(1);

        var response = await client.PatchAsJsonAsync(path, new { startsAt, endsAt, original.Version });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<CourseIntakeDetailDto>())!;
        Assert.Equal(startsAt, updated.StartsAt);
        Assert.Equal(endsAt, updated.EndsAt);
        Assert.Equal("InProgress", updated.Status);
        Assert.NotEqual(original.Version, updated.Version);
        Assert.Equal(original.RegistrationClosesAt, updated.RegistrationClosesAt);
        Assert.Equal(1, updated.ActiveEnrollmentCount);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var session = await db.CourseSessions.SingleAsync(s => s.CourseIntakeId == original.Id);
        Assert.Equal(original.StartsAt.AddHours(1), session.StartsAt);
        Assert.Equal(EnrollmentStatus.Active, (await db.Enrollments.SingleAsync(e => e.CourseSessionId == session.Id)).Status);
        var audit = await db.AuditLogs.SingleAsync(a => a.Action == "CourseIntakeTeachingDatesUpdated" && a.EntityId == original.Id.ToString());
        Assert.Equal(asAdmin, audit.AdminAccountId.HasValue);
        Assert.Equal(asAdmin ? null : (int?)setup.Staff.Id, audit.StaffAccountId);
        Assert.Null(audit.UserId);
        Assert.Contains(original.StartsAt.ToString("O"), audit.Reason);

        var stale = await client.PatchAsJsonAsync(path, new { startsAt, endsAt, original.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("INTAKE_VERSION_CONFLICT", (await stale.Content.ReadFromJsonAsync<ApiError>())!.Code);
        var detailPath = asAdmin ? $"/api/admin/courses/intakes/{original.Id}" : $"/api/staff/intakes/{original.Id}";
        Assert.Equal(updated.Version, (await client.GetFromJsonAsync<CourseIntakeDetailDto>(detailPath))!.Version);
    }

    [Theory]
    [InlineData("order", "INVALID_INTAKE_DATES")]
    [InlineData("registration", "INVALID_INTAKE_DATES")]
    [InlineData("session", "SESSION_OUTSIDE_INTAKE")]
    [InlineData("utc", "INVALID_UTC_TIME")]
    public async Task Invalid_ranges_are_rejected_without_persisting_changes(string scenario, string code)
    {
        var setup = await CreateIntakeAsync(CourseIntakeStatus.Published);
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        var original = setup.Intake;
        var startsAt = scenario == "registration" ? original.RegistrationClosesAt.AddHours(-1)
            : scenario == "session" ? original.StartsAt.AddHours(2) : original.StartsAt;
        var endsAt = scenario == "order" ? startsAt : original.EndsAt;
        if (scenario == "utc") startsAt = DateTime.SpecifyKind(startsAt, DateTimeKind.Unspecified);
        var response = await admin.PatchAsJsonAsync(Path(true, original.Id), new { startsAt, endsAt, original.Version });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>().CourseIntakes.FindAsync(original.Id);
        Assert.Equal(original.StartsAt, saved!.StartsAt);
        Assert.Equal(original.Version, saved.Version);
    }

    [Theory]
    [InlineData(CourseIntakeStatus.Completed)]
    [InlineData(CourseIntakeStatus.Cancelled)]
    [InlineData(CourseIntakeStatus.PendingApproval)]
    public async Task Closed_or_pending_classes_cannot_change_dates(CourseIntakeStatus status)
    {
        var setup = await CreateIntakeAsync(status);
        using var staff = StaffClient(setup.Staff);
        var response = await staff.PatchAsJsonAsync(Path(false, setup.Intake.Id), new
        {
            setup.Intake.StartsAt, endsAt = setup.Intake.EndsAt.AddDays(1), setup.Intake.Version,
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INTAKE_NOT_EDITABLE", (await response.Content.ReadFromJsonAsync<ApiError>())!.Code);
    }

    [Fact]
    public async Task Date_management_enforces_roles_ownership_and_allowed_fields()
    {
        var setup = await CreateIntakeAsync(CourseIntakeStatus.Published);
        var other = await CreateIntakeAsync(CourseIntakeStatus.Published);
        using var staff = StaffClient(other.Staff);
        var body = new { setup.Intake.StartsAt, endsAt = setup.Intake.EndsAt.AddDays(1), setup.Intake.Version };
        Assert.Equal(HttpStatusCode.NotFound, (await staff.PatchAsJsonAsync(Path(false, setup.Intake.Id), body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.PatchAsJsonAsync(Path(true, setup.Intake.Id), body)).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PatchAsJsonAsync(Path(false, setup.Intake.Id), body)).StatusCode);
        using var member = await ApiClient.AsMemberAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.PatchAsJsonAsync(Path(false, setup.Intake.Id), body)).StatusCode);
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        var overpost = await admin.PatchAsJsonAsync(Path(true, setup.Intake.Id), new { body.StartsAt, body.endsAt, body.Version, status = "Completed" });
        Assert.Equal(HttpStatusCode.BadRequest, overpost.StatusCode);
        var listed = await admin.GetAsync("/api/admin/courses/intakes");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
    }

    private static string Path(bool admin, int id) => admin
        ? $"/api/admin/courses/intakes/{id}/teaching-dates" : $"/api/staff/intakes/{id}/teaching-dates";

    [Fact]
    public async Task Admin_can_manage_creator_classes_while_staff_remains_scoped_to_platform_classes()
    {
        var setup = await CreateIntakeAsync(CourseIntakeStatus.Published);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>().Courses
                .Where(c => c.Id == setup.Intake.CourseId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPlatformOwned, false));
        var body = new { setup.Intake.StartsAt, endsAt = setup.Intake.EndsAt.AddDays(1), setup.Intake.Version };
        using var staff = StaffClient(setup.Staff);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.PatchAsJsonAsync(Path(false, setup.Intake.Id), body)).StatusCode);
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync(Path(true, setup.Intake.Id), body)).StatusCode);
    }

    [Fact]
    public async Task Inactive_staff_cannot_update_dates_with_an_existing_token()
    {
        var setup = await CreateIntakeAsync(CourseIntakeStatus.Published);
        using var staff = StaffClient(setup.Staff);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>().StaffAccounts
                .Where(s => s.Id == setup.Staff.Id).ExecuteUpdateAsync(s => s.SetProperty(s => s.IsActive, false));
        var response = await staff.PatchAsJsonAsync(Path(false, setup.Intake.Id), new
        {
            setup.Intake.StartsAt, endsAt = setup.Intake.EndsAt.AddDays(1), setup.Intake.Version,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Published_class_with_reserved_places_can_extend_its_dates()
    {
        var setup = await CreateIntakeAsync(CourseIntakeStatus.Published);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>().Enrollments
                .Where(e => e.CourseSession.CourseIntakeId == setup.Intake.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EnrollmentStatus.Reserved));
        using var staff = StaffClient(setup.Staff);
        var response = await staff.PatchAsJsonAsync(Path(false, setup.Intake.Id), new
        {
            setup.Intake.StartsAt, endsAt = setup.Intake.EndsAt.AddDays(1), setup.Intake.Version,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CourseIntakeDetailDto>())!;
        Assert.Equal("Published", result.Status);
        Assert.Equal(1, result.ReservedEnrollmentCount);
    }

    private HttpClient StaffClient(StaffAccount staff)
    {
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IStaffTokenService>().CreateToken(staff).Token;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<(CourseIntake Intake, StaffAccount Staff)> CreateIntakeAsync(CourseIntakeStatus status)
    {
        // Start the isolated test host before accessing its seeded database.
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var number = await db.StaffAccounts.CountAsync() + 100;
        var staff = new StaffAccount
        {
            Email = $"dates-{number}@example.test", FullName = "Dates staff", DisplayName = $"CoLearnX {number}",
            StaffNumber = number, SessionStamp = Guid.NewGuid(), CreatedByAdminAccountId = await db.AdminAccounts.Select(a => a.Id).FirstAsync(),
            User = new User { Email = $"dates-{number}@staff.colearnx", FullName = "Dates staff", DisplayName = "Dates staff",
                Roles = [new UserRole { Role = AppRole.Trainer }], TrainerProfile = new TrainerProfile() },
        };
        db.StaffAccounts.Add(staff);
        await db.SaveChangesAsync();
        var start = DateTime.UtcNow.Date.AddDays(-2);
        var course = new Course { Code = $"DATES-{number}", Title = "Dates workshop", TrainerId = staff.UserId,
            CreatorId = await db.Users.Where(u => u.Roles.Any(r => r.Role == AppRole.Creator)).Select(u => u.Id).FirstAsync(),
            IsPlatformOwned = true, Status = CourseStatus.Published };
        var intake = new CourseIntake { Course = course, TrainerId = staff.UserId, StartsAt = start, EndsAt = start.AddDays(5),
            RegistrationOpensAt = start.AddDays(-10), RegistrationClosesAt = start.AddDays(-5), Status = status,
            ConfirmedToRunAt = start.AddDays(-4), Sessions = [new CourseSession { Label = "Workshop", StartsAt = start.AddHours(1),
                EndsAt = start.AddHours(3), MeetingLink = "https://meet.example.test/dates" }] };
        db.CourseIntakes.Add(intake);
        await db.SaveChangesAsync();
        db.Enrollments.Add(new Enrollment { CourseId = course.Id, CourseSessionId = intake.Sessions.Single().Id,
            UserId = await db.Users.Where(u => u.Roles.Any(r => r.Role == AppRole.Member)).Select(u => u.Id).FirstAsync(), Status = EnrollmentStatus.Active });
        await db.SaveChangesAsync();
        return (intake, staff);
    }
}
