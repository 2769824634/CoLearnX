using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

// The Creator closes a class once its last session has ended.
public sealed class CreatorClassCompletionTests
{
    [Fact]
    public async Task Creator_completes_a_running_class_after_its_last_session()
    {
        using var factory = new CoLearnXApiFactory();
        using var creator = await ApiClient.AsCreatorAsync(factory);
        var (intakeId, trainerId) = await RunningClass(factory, lastSessionEndedHoursAgo: 2);

        var response = await creator.PostAsync($"/api/creator/intakes/{intakeId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("Completed", body["status"]!.GetValue<string>());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(CourseIntakeStatus.Completed, (await db.CourseIntakes.AsNoTracking().SingleAsync(i => i.Id == intakeId)).Status);
        Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == "CourseIntakeCompleted" && log.EntityId == intakeId.ToString()));
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == trainerId && n.IntakeId == intakeId && n.Code == "N-intake-completed"));
    }

    [Fact]
    public async Task Completing_twice_changes_nothing_the_second_time()
    {
        using var factory = new CoLearnXApiFactory();
        using var creator = await ApiClient.AsCreatorAsync(factory);
        var (intakeId, _) = await RunningClass(factory, lastSessionEndedHoursAgo: 2);

        (await creator.PostAsync($"/api/creator/intakes/{intakeId}/complete", null)).EnsureSuccessStatusCode();
        var again = await creator.PostAsync($"/api/creator/intakes/{intakeId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.Action == "CourseIntakeCompleted" && log.EntityId == intakeId.ToString()));
    }

    [Fact]
    public async Task A_class_cannot_be_completed_before_its_last_session_ends()
    {
        using var factory = new CoLearnXApiFactory();
        using var creator = await ApiClient.AsCreatorAsync(factory);
        var (intakeId, _) = await RunningClass(factory, lastSessionEndedHoursAgo: -3);

        var response = await creator.PostAsync($"/api/creator/intakes/{intakeId}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CLASS_NOT_FINISHED", (await ApiClient.ReadErrorAsync(response))!.Code);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(CourseIntakeStatus.InProgress, (await db.CourseIntakes.AsNoTracking().SingleAsync(i => i.Id == intakeId)).Status);
    }

    [Theory]
    [InlineData(CourseIntakeStatus.Draft)]
    [InlineData(CourseIntakeStatus.Cancelled)]
    public async Task Only_a_class_that_ran_can_be_completed(CourseIntakeStatus status)
    {
        using var factory = new CoLearnXApiFactory();
        using var creator = await ApiClient.AsCreatorAsync(factory);
        var (intakeId, _) = await RunningClass(factory, lastSessionEndedHoursAgo: 2, status);

        var response = await creator.PostAsync($"/api/creator/intakes/{intakeId}/complete", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INTAKE_NOT_COMPLETABLE", (await ApiClient.ReadErrorAsync(response))!.Code);
    }

    [Fact]
    public async Task Only_the_owning_Creator_can_complete_a_class()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var creator = await ApiClient.AsCreatorAsync(factory);
        var (intakeId, _) = await RunningClass(factory, lastSessionEndedHoursAgo: 2);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var other = new User { Email = "other-creator@example.com", FullName = "Other Creator", IsActive = true };
            db.Users.Add(other);
            await db.SaveChangesAsync();
            var intake = await db.CourseIntakes.Include(i => i.Course).SingleAsync(i => i.Id == intakeId);
            intake.Course.CreatorId = other.Id;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync($"/api/creator/intakes/{intakeId}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await creator.PostAsync($"/api/creator/intakes/{intakeId}/complete", null)).StatusCode);
    }

    private static async Task<(int IntakeId, int TrainerId)> RunningClass(CoLearnXApiFactory factory, int lastSessionEndedHoursAgo,
        CourseIntakeStatus status = CourseIntakeStatus.InProgress)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var creatorId = await db.Users.Where(u => u.Email == SeedData.CreatorEmail).Select(u => u.Id).SingleAsync();
        var intake = await db.CourseIntakes.Include(i => i.Sessions)
            .Where(i => i.Course.CreatorId == creatorId).OrderBy(i => i.Id).FirstAsync();
        var lastEnd = DateTime.UtcNow.AddHours(-lastSessionEndedHoursAgo);
        intake.Status = status;
        intake.CancelledAt = status == CourseIntakeStatus.Cancelled ? DateTime.UtcNow : null;
        intake.ConfirmedToRunAt = DateTime.UtcNow.AddDays(-20);
        intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-40);
        intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(-21);
        intake.StartsAt = lastEnd.AddDays(-14);
        intake.EndsAt = lastEnd.AddDays(1);
        var offset = 0;
        foreach (var session in intake.Sessions.OrderByDescending(s => s.StartsAt))
        {
            session.EndsAt = lastEnd.AddDays(-7 * offset);
            session.StartsAt = session.EndsAt.AddHours(-2);
            offset++;
        }
        await db.SaveChangesAsync();
        return (intake.Id, intake.TrainerId);
    }
}
