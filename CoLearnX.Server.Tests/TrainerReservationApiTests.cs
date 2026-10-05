using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class TrainerReservationApiTests
{
    [Fact]
    public async Task Owned_intake_returns_reservation_progress_and_reserved_rows_only()
    {
        using var factory = new CoLearnXApiFactory();
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(factory);
        var intakeId = await IntakeNotificationTests.SeedDueReservation(factory);
        int reservedId;
        int activeId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(item => item.Sessions).SingleAsync(item => item.Id == intakeId);
            var session = intake.Sessions.Single();
            reservedId = await db.Enrollments.Where(item => item.CourseSessionId == session.Id && item.Status == EnrollmentStatus.Reserved)
                .Select(item => item.Id).SingleAsync();
            var activeUser = new User { Email = $"reservation-active-{Guid.NewGuid():N}@example.test", FullName = "Active Learner" };
            db.Users.Add(activeUser);
            await db.SaveChangesAsync();
            var active = new Enrollment
            {
                UserId = activeUser.Id,
                CourseId = intake.CourseId,
                CourseSessionId = session.Id,
                Status = EnrollmentStatus.Active,
                CreditsSpent = 35,
            };
            db.Enrollments.Add(active);
            await db.SaveChangesAsync();
            activeId = active.Id;
            intake.MinEnrollment = 3;
            await db.SaveChangesAsync();
        }

        using var detailResponse = await trainer.GetAsync($"/api/trainer/intakes/{intakeId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(1, detail!["reservedEnrollmentCount"]!.GetValue<int>());
        Assert.Equal(1, detail["activeEnrollmentCount"]!.GetValue<int>());
        Assert.Equal(1, detail["remainingToMinimum"]!.GetValue<int>());

        using var reservationsResponse = await trainer.GetAsync($"/api/trainer/intakes/{intakeId}/reservations");
        Assert.Equal(HttpStatusCode.OK, reservationsResponse.StatusCode);
        var rows = (await reservationsResponse.Content.ReadFromJsonAsync<JsonArray>())!;
        var row = Assert.Single(rows);
        Assert.Equal(reservedId, row!["enrollmentId"]!.GetValue<int>());
        Assert.True(row["courseSessionId"]!.GetValue<int>() > 0);
        Assert.Equal("Session 1", row["sessionLabel"]!.GetValue<string>());
        Assert.Equal("Huang Yousheng", row["learnerName"]!.GetValue<string>());
        Assert.Equal(35, row["creditsHeld"]!.GetValue<int>());
        Assert.True(row["reservedAt"]!.GetValue<DateTime>() != default);

        using var memberResponse = await member.GetAsync($"/api/trainer/intakes/{intakeId}/reservations");
        Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);
        Assert.True(activeId > 0);
    }

    [Fact]
    public async Task A_different_trainer_or_missing_intake_cannot_read_reservations()
    {
        using var factory = new CoLearnXApiFactory();
        var intakeId = await IntakeNotificationTests.SeedDueReservation(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var creatorId = await db.Users.Where(item => item.Email == SeedData.CreatorEmail).Select(item => item.Id).SingleAsync();
            if (!await db.UserRoles.AnyAsync(item => item.UserId == creatorId && item.Role == AppRole.Trainer))
                db.UserRoles.Add(new UserRole { UserId = creatorId, Role = AppRole.Trainer });
            await db.SaveChangesAsync();
        }

        using var otherTrainer = await ApiClient.AsRoleAsync(factory, SeedData.CreatorEmail, "Trainer");
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherTrainer.GetAsync($"/api/trainer/intakes/{intakeId}/reservations")).StatusCode);
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        Assert.Equal(HttpStatusCode.NotFound,
            (await trainer.GetAsync("/api/trainer/intakes/2147483647/reservations")).StatusCode);
    }
}
