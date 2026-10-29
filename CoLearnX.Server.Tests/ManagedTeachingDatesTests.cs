using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class ManagedTeachingDatesTests(CoLearnXApiFactory factory) : IClassFixture<CoLearnXApiFactory>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Admin_and_owning_staff_save_dates_without_changing_sessions_or_registration(bool admin)
    {
        var item = await CreateClassAsync();
        using var client = await ClientAsync(item, admin);
        var response = await client.PatchAsJsonAsync(Url(item, admin), new
        {
            startsAt = item.Start.AddDays(-1), endsAt = item.Start.AddDays(4), version = item.Version,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CourseIntakeDetailDto>())!;
        Assert.Equal(item.Start.AddDays(-1), result.StartsAt);
        Assert.Equal(item.Start.AddDays(4), result.EndsAt);
        Assert.Equal(item.Start.AddHours(9), Assert.Single(result.Sessions).StartsAt);
        Assert.Equal(item.Start.AddDays(-5), result.RegistrationClosesAt);
        Assert.Equal("Published", result.Status);
        Assert.NotEqual(item.Version, result.Version);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var log = await db.AuditLogs.SingleAsync(x => x.Action == "TeachingDatesUpdated" && x.EntityId == item.IntakeId.ToString());
        Assert.Equal(admin ? item.AdminId : null, log.AdminAccountId);
        Assert.Equal(admin ? null : item.StaffId, log.StaffAccountId);
        Assert.Contains("startsAt", log.Reason);
        var replay = await client.PatchAsJsonAsync(Url(item, admin), new
        {
            startsAt = item.Start.AddDays(-1), endsAt = item.Start.AddDays(4), version = item.Version,
        });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        Assert.Equal("INTAKE_VERSION_CONFLICT", (await ApiClient.ReadErrorAsync(replay))?.Code);
    }

    [Theory]
    [InlineData(0, "INVALID_INTAKE_DATES")]
    [InlineData(1, "SESSION_OUTSIDE_INTAKE")]
    [InlineData(2, "INVALID_INTAKE_DATES")]
    public async Task Invalid_dates_do_not_write_or_audit(int scenario, string code)
    {
        var item = await CreateClassAsync();
        using var client = await ClientAsync(item, true);
        var starts = scenario == 2 ? item.Start.AddDays(-6) : item.Start;
        var ends = scenario == 0 ? starts : scenario == 1 ? item.Start.AddHours(10) : item.Start.AddDays(2);
        var response = await client.PatchAsJsonAsync(Url(item, true), new { startsAt = starts, endsAt = ends, version = item.Version });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, (await ApiClient.ReadErrorAsync(response))?.Code);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(item.Version, (await db.CourseIntakes.FindAsync(item.IntakeId))!.Version);
        Assert.False(await db.AuditLogs.AnyAsync(x => x.Action == "TeachingDatesUpdated" && x.EntityId == item.IntakeId.ToString()));
    }

    [Theory]
    [InlineData(CourseIntakeStatus.Completed)]
    [InlineData(CourseIntakeStatus.Cancelled)]
    [InlineData(CourseIntakeStatus.PendingApproval)]
    public async Task Closed_or_pending_classes_cannot_change_dates(CourseIntakeStatus status)
    {
        var item = await CreateClassAsync(status);
        using var client = await ClientAsync(item, true);
        var response = await client.PatchAsJsonAsync(Url(item, true), new
        {
            startsAt = item.Start, endsAt = item.Start.AddDays(4), version = item.Version,
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INTAKE_NOT_EDITABLE", (await ApiClient.ReadErrorAsync(response))?.Code);
    }

    [Fact]
    public async Task Staff_cannot_edit_another_staffs_class_and_member_cannot_use_admin_schedule()
    {
        var owner = await CreateClassAsync();
        var other = await CreateClassAsync();
        using var staff = await ClientAsync(other, false);
        var response = await staff.PatchAsJsonAsync(Url(owner, false), new
        {
            startsAt = owner.Start, endsAt = owner.Start.AddDays(4), version = owner.Version,
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var member = await ApiClient.AsMemberAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync($"/api/admin/courses/{owner.CourseId}/schedule")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync($"/api/admin/courses/{owner.CourseId}/schedule")).StatusCode);
    }

    [Fact]
    public async Task Admin_lists_only_the_requested_courses_classes()
    {
        var item = await CreateClassAsync();
        using var client = await ClientAsync(item, true);
        var response = await client.GetAsync($"/api/admin/courses/{item.CourseId}/schedule");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var classes = (await response.Content.ReadFromJsonAsync<CourseIntakeDetailDto[]>())!;
        Assert.Equal(item.IntakeId, Assert.Single(classes).Id);
    }

    private static string Url(ClassData item, bool admin) => admin
        ? $"/api/admin/courses/{item.CourseId}/schedule/{item.IntakeId}"
        : $"/api/staff/intakes/{item.IntakeId}/teaching-dates";

    [Fact]
    public async Task Started_classes_can_extend_the_end_but_cannot_rewrite_the_start()
    {
        var item = await CreateClassAsync(CourseIntakeStatus.InProgress);
        var start = DateTime.UtcNow.Date.AddDays(-1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(x => x.Sessions).SingleAsync(x => x.Id == item.IntakeId);
            intake.RegistrationOpensAt = start.AddDays(-10); intake.RegistrationClosesAt = start.AddDays(-5);
            intake.StartsAt = start; intake.EndsAt = start.AddDays(2);
            var session = Assert.Single(intake.Sessions);
            session.StartsAt = start.AddHours(9); session.EndsAt = start.AddHours(11);
            await db.SaveChangesAsync();
        }
        using var client = await ClientAsync(item, true);
        var rewrite = await client.PatchAsJsonAsync(Url(item, true), new { startsAt = start.AddDays(-1), endsAt = start.AddDays(4), version = item.Version });
        Assert.Equal(HttpStatusCode.Conflict, rewrite.StatusCode);
        Assert.Equal("TEACHING_ALREADY_STARTED", (await ApiClient.ReadErrorAsync(rewrite))?.Code);
        var extend = await client.PatchAsJsonAsync(Url(item, true), new { startsAt = start, endsAt = start.AddDays(4), version = item.Version });
        Assert.Equal(HttpStatusCode.OK, extend.StatusCode);
    }

    [Fact]
    public async Task Missing_version_unknown_fields_and_non_UTC_dates_are_rejected()
    {
        var item = await CreateClassAsync();
        using var client = await ClientAsync(item, true);
        var missing = await client.PatchAsJsonAsync(Url(item, true), new { startsAt = item.Start, endsAt = item.Start.AddDays(4) });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var extra = await client.PatchAsJsonAsync(Url(item, true), new { startsAt = item.Start, endsAt = item.Start.AddDays(4), version = item.Version, status = "Completed" });
        Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);
        var local = await client.PatchAsJsonAsync(Url(item, true), new { startsAt = item.Start.ToString("yyyy-MM-ddTHH:mm:ss"), endsAt = item.Start.AddDays(4), version = item.Version });
        Assert.Equal(HttpStatusCode.BadRequest, local.StatusCode);
        Assert.Equal("INVALID_UTC_TIME", (await ApiClient.ReadErrorAsync(local))?.Code);
    }

    private async Task<HttpClient> ClientAsync(ClassData item, bool admin)
    {
        if (admin) return await ApiClient.AsOperationsAdminAsync(factory);
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/staff/auth/login", new { email = item.Email, password = SeedData.DemoPassword });
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<StaffAuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private async Task<ClassData> CreateClassAsync(CourseIntakeStatus status = CourseIntakeStatus.Published)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var admin = await db.AdminAccounts.FirstAsync();
        var email = $"dates-{Guid.NewGuid():N}@example.test";
        var user = new User { Email = email, FullName = "Schedule Trainer", IsActive = true,
            Roles = [new UserRole { Role = AppRole.Trainer }] };
        var staff = new StaffAccount { Email = email, User = user, FullName = user.FullName,
            StaffNumber = 100000 + await db.StaffAccounts.CountAsync(), CreatedByAdminAccountId = admin.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedData.DemoPassword, 4) };
        var course = new Course { Code = $"DATE-{Guid.NewGuid():N}", Title = "Teaching dates test", Creator = user,
            Trainer = user, IsPlatformOwned = true, Status = CourseStatus.Published };
        var start = DateTime.UtcNow.Date.AddDays(30);
        var intake = new CourseIntake { Course = course, Trainer = user, StartsAt = start, EndsAt = start.AddDays(2),
            RegistrationOpensAt = start.AddDays(-10), RegistrationClosesAt = start.AddDays(-5), Status = status,
            Sessions = [new CourseSession { Label = "Session", StartsAt = start.AddHours(9), EndsAt = start.AddHours(11), MeetingLink = "https://example.test/session" }] };
        db.StaffAccounts.Add(staff);
        db.CourseIntakes.Add(intake);
        await db.SaveChangesAsync();
        return new(intake.Id, course.Id, staff.Id, admin.Id, email, start, intake.Version);
    }

    private sealed record ClassData(int IntakeId, int CourseId, int StaffId, int AdminId, string Email, DateTime Start, Guid Version);
}
