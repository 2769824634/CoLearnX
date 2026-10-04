using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using CoLearnX.Server.Contracts.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class IntakeNotificationTests
{
    [Fact]
    public async Task FillingAndReleasingASeatLinksTrainerToTheSameIntake()
    {
        using var factory = new CoLearnXApiFactory();
        var id = await SeedDueReservation(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Id == id);
        intake.MinEnrollment = 2;
        intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(2);
        var session = intake.Sessions.Single();
        session.PhysicalCapacity = 2;
        session.PhysicalBookingDeadline = intake.RegistrationClosesAt;
        var user = new User { Email = "seat-notification@example.test", CreditBalance = 100 };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var enrolments = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
        var reserved = await enrolments.EnrolAsync(user.Id, new EnrolRequest(intake.CourseId, session.Id));
        await enrolments.CancelReservationAsync(user.Id, reserved.EnrollmentId);
        var inbox = await scope.ServiceProvider.GetRequiredService<NotificationService>().GetMyAsync(intake.TrainerId, CancellationToken.None);
        foreach (var code in new[] { "N-session-full", "N-min-reached", "N-session-reopened" })
        {
            var item = Assert.Single(inbox.Items, n => n.Type == code);
            Assert.Equal(id, item.IntakeId);
            Assert.Equal($"/trainer/courses/intakes/{id}", item.TargetPath);
        }
    }

    [Fact]
    public async Task MinimumCancellationExplainsWindowAndLinksEachRoleToItsIntake()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var creator = await ApiClient.AsRoleAsync(factory, SeedData.CreatorEmail, "Creator");
        var id = await SeedDueReservation(factory);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(id);
        var learner = await member.GetFromJsonAsync<JsonObject>("/api/notifications/my");
        var released = learner!["items"]!.AsArray().Single(n => n!["type"]!.GetValue<string>() == "N-hold-released");
        Assert.Contains("7 days", released!["message"]!.GetValue<string>());
        Assert.Contains("reserve again", released["message"]!.GetValue<string>());
        foreach (var (client, code, path) in new[] {
            (trainer, "N-intake-cancelled", $"/trainer/courses/intakes/{id}"),
            (creator, "N-intake-cancelled-creator", $"/creator/courses/intake-applications/{id}") })
        {
            var inbox = await client.GetFromJsonAsync<JsonObject>("/api/notifications/my");
            var notice = inbox!["items"]!.AsArray().Single(n => n!["type"]!.GetValue<string>() == code);
            Assert.Equal(path, notice!["targetPath"]!.GetValue<string>());
            Assert.Equal(id, notice["intakeId"]?.GetValue<int>());
        }
        var creatorDetail = await creator.GetAsync($"/api/creator/intake-applications/{id}");
        Assert.True(creatorDetail.IsSuccessStatusCode, await creatorDetail.Content.ReadAsStringAsync());
        var detail = await creatorDetail.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("Cancelled", detail!["currentIntake"]!["status"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("N-01", "/member/programs?tab=reserved")]
    [InlineData("N-class-confirmed", "/member/programs?tab=active")]
    [InlineData("N-class-cancelled", "/member/programs?tab=history")]
    [InlineData("N-postponement-offered", "/member/programs?tab=history")]
    public async Task LearnerCancellationAndReplacementEventsHaveDestinations(string code, string path)
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == SeedData.MemberEmail);
        db.Notifications.Add(new Notification { UserId = user.Id, Code = code, Title = code, Body = "Business event" });
        await db.SaveChangesAsync();
        var inbox = await member.GetFromJsonAsync<JsonObject>("/api/notifications/my");
        Assert.Equal(path, inbox!["items"]!.AsArray().Single(n => n!["type"]!.GetValue<string>() == code)!["targetPath"]?.GetValue<string>());
    }

    internal static async Task<int> SeedDueReservation(CoLearnXApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
        intake.Status = CourseIntakeStatus.Published;
        intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-3);
        intake.RegistrationClosesAt = DateTime.UtcNow.AddMinutes(-1);
        intake.StartsAt = DateTime.UtcNow.AddDays(20);
        intake.EndsAt = intake.StartsAt.AddDays(1);
        intake.MinEnrollment = 10;
        var user = await db.Users.SingleAsync(u => u.Email == SeedData.MemberEmail);
        user.HeldCredits = 35;
        user.CreditBalance = 85;
        var session = intake.Sessions.Single();
        session.StartsAt = intake.StartsAt;
        session.EndsAt = intake.StartsAt.AddHours(2);
        session.SeatsTaken = 1;
        db.Enrollments.Add(new Enrollment { UserId = user.Id, CourseId = intake.CourseId, CourseSessionId = session.Id, Status = EnrollmentStatus.Reserved, CreditsSpent = 35 });
        await db.SaveChangesAsync();
        return intake.Id;
    }
}
