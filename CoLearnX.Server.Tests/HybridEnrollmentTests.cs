using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class HybridEnrollmentTests
{
    [Fact]
    public async Task Existing_sqlite_database_can_add_recruitment_and_session_choices_twice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE Courses (Id INTEGER PRIMARY KEY);
            CREATE TABLE Enrollments (Id INTEGER PRIMARY KEY);
            CREATE TABLE CourseSessions (Id INTEGER PRIMARY KEY);
            INSERT INTO Courses VALUES (7);
            INSERT INTO Enrollments VALUES (8);
            INSERT INTO CourseSessions VALUES (9);
            """);

        await CourseRecruitmentSchema.EnsureAsync(db);
        await EnrollmentSessionChoiceSchema.EnsureAsync(db);
        await CourseRecruitmentSchema.EnsureAsync(db);
        await EnrollmentSessionChoiceSchema.EnsureAsync(db);
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT OpenToTrainerApplications AS Value FROM Courses WHERE Id = 7").SingleAsync());
        await db.Database.ExecuteSqlRawAsync("INSERT INTO EnrollmentSessionChoices (EnrollmentId, CourseSessionId, Mode) VALUES (8, 9, 'Online')");
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM EnrollmentSessionChoices").SingleAsync());
    }

    [Fact]
    public async Task Online_choice_drives_the_class_reminder_and_grants_access_to_chosen_session_recordings()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int enrollmentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
            intake.Status = CourseIntakeStatus.Published;
            intake.ConfirmedToRunAt = DateTime.UtcNow.AddDays(-1);
            intake.StartsAt = DateTime.UtcNow.AddDays(5);
            intake.EndsAt = intake.StartsAt.AddDays(2);
            var first = intake.Sessions.Single();
            first.StartsAt = intake.StartsAt; first.EndsAt = intake.StartsAt.AddHours(2);
            first.MeetingLink = "https://example.test/online";
            first.PhysicalAddress = "Room 1"; first.PhysicalCapacity = 3;
            var second = new CourseSession { CourseIntakeId = intake.Id, Label = "Session 2",
                StartsAt = intake.StartsAt.AddDays(1), EndsAt = intake.StartsAt.AddDays(1).AddHours(2),
                MeetingLink = "https://example.test/second" };
            db.CourseSessions.Add(second);
            await db.SaveChangesAsync();
            var memberId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
            var enrollment = new Enrollment { UserId = memberId, CourseId = intake.CourseId,
                CourseSessionId = first.Id, Status = EnrollmentStatus.Active, CreditsSpent = 60 };
            enrollment.DeliveryChoices.Add(new EnrollmentSessionChoice { CourseSessionId = first.Id, Mode = "Online" });
            enrollment.DeliveryChoices.Add(new EnrollmentSessionChoice { CourseSessionId = second.Id, Mode = "Online" });
            db.Enrollments.Add(enrollment);
            db.SessionRecordings.Add(new SessionRecording { CourseSessionId = second.Id, Title = "Second recording",
                RecordingUrl = "https://example.test/recording" });
            await db.SaveChangesAsync();
            enrollmentId = enrollment.Id;
            await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().ProcessDueAsync();
            var reminder = await db.Notifications.SingleAsync(n => n.EnrollmentId == enrollmentId && n.Code == "N-class-reminder");
            Assert.Contains("https://example.test/online", reminder.Body);
            Assert.DoesNotContain("Room 1", reminder.Body);
        }

        var recordings = await member.GetFromJsonAsync<JsonArray>($"/api/enrollments/{enrollmentId}/recordings");
        Assert.Contains(recordings!, item => item!["title"]!.GetValue<string>() == "Second recording");
    }

    [Fact]
    public async Task Learner_can_choose_a_mode_per_session_and_cancel_without_leaking_seats_or_credits()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int courseId, firstId, secondId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
            intake.Status = CourseIntakeStatus.Published;
            intake.MinEnrollment = 2;
            intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-1);
            intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(3);
            intake.StartsAt = DateTime.UtcNow.AddDays(10);
            intake.EndsAt = intake.StartsAt.AddDays(2);
            var first = intake.Sessions.Single();
            first.StartsAt = intake.StartsAt; first.EndsAt = intake.StartsAt.AddHours(2);
            first.MeetingLink = "https://example.test/online"; first.PhysicalAddress = "Room 1";
            first.PhysicalCapacity = 2; first.PhysicalBookingDeadline = intake.RegistrationClosesAt; first.SeatsTaken = 2;
            var second = new CourseSession { CourseIntakeId = intake.Id, Label = "Session 2", StartsAt = intake.StartsAt.AddDays(1),
                EndsAt = intake.StartsAt.AddDays(1).AddHours(2), MeetingLink = "https://example.test/online",
                PhysicalAddress = "Room 2", PhysicalCapacity = 2, PhysicalBookingDeadline = intake.RegistrationClosesAt };
            db.CourseSessions.Add(second);
            await db.SaveChangesAsync();
            courseId = intake.CourseId; firstId = first.Id; secondId = second.Id;
        }
        var before = (await member.GetFromJsonAsync<JsonObject>("/api/auth/me"))!["creditBalance"]!.GetValue<int>();
        var incomplete = await member.PostAsJsonAsync("/api/enrollments", new {
            courseId, courseSessionId = firstId, deliveryChoices = new[] { new { courseSessionId = firstId, mode = "Online" } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Equal("SESSION_CHOICES_REQUIRED", (await incomplete.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>());
        var full = await member.PostAsJsonAsync("/api/enrollments", new {
            courseId, courseSessionId = firstId,
            deliveryChoices = new[] { new { courseSessionId = firstId, mode = "Physical" }, new { courseSessionId = secondId, mode = "Physical" } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, full.StatusCode);
        Assert.Equal("SESSION_FULL", (await full.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>());
        Assert.Equal(before, (await member.GetFromJsonAsync<JsonObject>("/api/auth/me"))!["creditBalance"]!.GetValue<int>());
        var fullOnline = await member.PostAsJsonAsync("/api/enrollments", new {
            courseId, courseSessionId = firstId,
            deliveryChoices = new[] { new { courseSessionId = firstId, mode = "Online" }, new { courseSessionId = secondId, mode = "Physical" } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, fullOnline.StatusCode);
        Assert.Equal("SESSION_FULL", (await fullOnline.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            (await db.CourseSessions.FindAsync(firstId))!.SeatsTaken = 1;
            await db.SaveChangesAsync();
        }
        var response = await member.PostAsJsonAsync("/api/enrollments", new {
            courseId, courseSessionId = firstId,
            deliveryChoices = new[] { new { courseSessionId = firstId, mode = "Online" }, new { courseSessionId = secondId, mode = "Physical" } },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var enrollmentId = created["enrollmentId"]!.GetValue<int>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(2, (await db.CourseSessions.FindAsync(firstId))!.SeatsTaken);
            Assert.Equal(1, (await db.CourseSessions.FindAsync(secondId))!.SeatsTaken);
        }
        var mine = await member.GetFromJsonAsync<JsonArray>("/api/enrollments/my");
        var item = mine!.Single(e => e!["id"]!.GetValue<int>() == enrollmentId)!;
        Assert.Equal(2, item["deliveryChoices"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.OK, (await member.PostAsync($"/api/enrollments/{enrollmentId}/cancel-reservation", null)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(1, (await db.CourseSessions.FindAsync(firstId))!.SeatsTaken);
            Assert.Equal(0, (await db.CourseSessions.FindAsync(secondId))!.SeatsTaken);
        }
        var after = (await member.GetFromJsonAsync<JsonObject>("/api/auth/me"))!["creditBalance"]!.GetValue<int>();
        Assert.Equal(before, after);
    }
}
