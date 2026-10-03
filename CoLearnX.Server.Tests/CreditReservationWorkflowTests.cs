using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class CreditReservationWorkflowTests
{
    [Fact]
    public async Task ScheduledTrainerWarningAndMemberReminderAreSentOnce()
    {
        using var factory = new CoLearnXApiFactory();
        int intakeId;
        int trainerId;
        int memberId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
            intakeId = intake.Id;
            trainerId = intake.TrainerId;
            memberId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
            intake.Status = CourseIntakeStatus.Published;
            intake.MinEnrollment = 2;
            intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-1);
            intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(2);
            intake.StartsAt = DateTime.UtcNow.AddDays(20);
            intake.EndsAt = intake.StartsAt.AddDays(1);
            await db.SaveChangesAsync();
        }
        using (var scope = factory.Services.CreateScope())
        {
            var settlement = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            await settlement.ProcessDueAsync();
            await settlement.ProcessDueAsync();
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == trainerId && n.Code == "N-under-enrolled"));
            var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Id == intakeId);
            intake.ConfirmedToRunAt = DateTime.UtcNow;
            intake.StartsAt = DateTime.UtcNow.AddDays(6);
            intake.EndsAt = intake.StartsAt.AddDays(1);
            var session = intake.Sessions.Single();
            session.StartsAt = intake.StartsAt;
            session.EndsAt = intake.StartsAt.AddHours(2);
            session.PhysicalCapacity = 0;
            session.MeetingLink = "https://example.test/class";
            db.Enrollments.Add(new Enrollment { UserId = memberId, CourseId = intake.CourseId,
                CourseSessionId = session.Id, Status = EnrollmentStatus.Active, CreditsSpent = 35 });
            await db.SaveChangesAsync();
        }
        using (var scope = factory.Services.CreateScope())
        {
            var settlement = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            await settlement.ProcessDueAsync();
            await settlement.ProcessDueAsync();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == memberId && n.Code == "N-class-reminder"));
            var reminder = await db.Notifications.SingleAsync(n => n.UserId == memberId && n.Code == "N-class-reminder");
            Assert.Contains("https://example.test/class", reminder.Body);
        }
    }

    [Fact]
    public async Task FullPhysicalSessionRejectsHoldWithoutMovingCredits()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int courseId;
        int sessionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
            intake.Status = CourseIntakeStatus.Published;
            intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-1);
            intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(5);
            intake.StartsAt = DateTime.UtcNow.AddDays(20);
            intake.EndsAt = intake.StartsAt.AddDays(1);
            var session = intake.Sessions.Single();
            session.StartsAt = intake.StartsAt;
            session.EndsAt = intake.StartsAt.AddHours(2);
            session.PhysicalAddress = "Room 1";
            session.PhysicalCapacity = 10;
            session.PhysicalBookingDeadline = DateTime.UtcNow.AddDays(5);
            session.SeatsTaken = 10;
            await db.SaveChangesAsync();
            courseId = intake.CourseId;
            sessionId = session.Id;
        }
        var before = await member.GetFromJsonAsync<JsonObject>("/api/auth/me");
        var response = await member.PostAsJsonAsync("/api/enrollments", new { courseId, courseSessionId = sessionId });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("SESSION_FULL", (await response.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>());
        var after = await member.GetFromJsonAsync<JsonObject>("/api/auth/me");
        Assert.Equal(before!["creditBalance"]!.GetValue<int>(), after!["creditBalance"]!.GetValue<int>());
        Assert.Equal(before["heldCredits"]!.GetValue<int>(), after["heldCredits"]!.GetValue<int>());
    }

    [Fact]
    public async Task TrainerCancellationBeforeConfirmationReleasesEveryReservation()
    {
        using var factory = new CoLearnXApiFactory();
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        int intakeId;
        int enrollmentId;
        int available;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
            intakeId = intake.Id;
            intake.Status = CourseIntakeStatus.Published;
            var user = await db.Users.SingleAsync(u => u.Email == SeedData.MemberEmail);
            available = user.CreditBalance;
            user.CreditBalance -= 35;
            user.HeldCredits += 35;
            var session = intake.Sessions.Single();
            session.SeatsTaken++;
            var enrollment = new Enrollment { UserId = user.Id, CourseId = intake.CourseId,
                CourseSessionId = session.Id, Status = EnrollmentStatus.Reserved, CreditsSpent = 35 };
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();
            enrollmentId = enrollment.Id;
        }
        var response = await trainer.PostAsync($"/api/trainer/intakes/{intakeId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var saved = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(CourseIntakeStatus.Cancelled, (await saved.CourseIntakes.FindAsync(intakeId))!.Status);
        Assert.Equal(EnrollmentStatus.Cancelled, (await saved.Enrollments.FindAsync(enrollmentId))!.Status);
        var wallet = await saved.Users.SingleAsync(u => u.Email == SeedData.MemberEmail);
        Assert.Equal(available, wallet.CreditBalance);
        Assert.Equal(0, wallet.HeldCredits);
        Assert.Single(await saved.CreditTransactions.Where(t => t.RelatedEnrollmentId == enrollmentId
            && t.Type == CreditTransactionType.Release).ToListAsync());
    }

    [Fact]
    public async Task ConfirmedMemberWithdrawalRefundsSeventyPercentAndRecordsForfeit()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int enrollmentId;
        int cost;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var enrollment = await db.Enrollments.Include(e => e.CourseSession).ThenInclude(s => s.CourseIntake)
                .SingleAsync(e => e.Status == EnrollmentStatus.Active && e.Course.Code == "INFT 3030");
            enrollmentId = enrollment.Id;
            cost = enrollment.CreditsSpent;
            var intake = enrollment.CourseSession.CourseIntake;
            intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-5);
            intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(-1);
            intake.StartsAt = DateTime.UtcNow.AddDays(8);
            intake.EndsAt = intake.StartsAt.AddDays(1);
            intake.ConfirmedToRunAt = DateTime.UtcNow.AddHours(-1);
            enrollment.CourseSession.StartsAt = intake.StartsAt;
            enrollment.CourseSession.EndsAt = intake.StartsAt.AddHours(2);
            await db.SaveChangesAsync();
        }
        var before = await member.GetFromJsonAsync<JsonObject>("/api/auth/me");
        var response = await member.PostAsync($"/api/enrollments/{enrollmentId}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await member.GetFromJsonAsync<JsonObject>("/api/auth/me");
        var refund = (int)Math.Round(cost * 0.7, MidpointRounding.AwayFromZero);
        Assert.Equal(before!["creditBalance"]!.GetValue<int>() + refund, after!["creditBalance"]!.GetValue<int>());
        var ledger = await member.GetFromJsonAsync<JsonArray>("/api/credits/ledger/my");
        Assert.Single(ledger!, item => item!["type"]!.GetValue<string>() == "Refund"
            && item["relatedEnrollmentId"]!.GetValue<int>() == enrollmentId);
        Assert.Single(ledger!, item => item!["type"]!.GetValue<string>() == "Forfeit"
            && item["relatedEnrollmentId"]!.GetValue<int>() == enrollmentId);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task DueIntakeReleasesOrCapturesTheWholeClassExactlyOnce(int reservedCount, bool runs)
    {
        using var factory = new CoLearnXApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
        intake.MinEnrollment = 2;
        intake.Status = CourseIntakeStatus.Published;
        intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-2);
        intake.RegistrationClosesAt = DateTime.UtcNow.AddMinutes(-1);
        intake.StartsAt = DateTime.UtcNow.AddDays(12);
        intake.EndsAt = intake.StartsAt.AddDays(1);
        var session = intake.Sessions.Single();
        session.StartsAt = intake.StartsAt;
        session.EndsAt = intake.StartsAt.AddHours(2);
        var member = await db.Users.SingleAsync(u => u.Email == SeedData.MemberEmail);
        var users = new List<User> { member };
        if (reservedCount == 2)
        {
            var other = new User { Email = $"settle-{Guid.NewGuid():N}@example.com", FullName = "Other Member",
                DisplayName = "Other", PasswordHash = "unused", CreditBalance = 80 };
            db.Users.Add(other);
            users.Add(other);
        }
        await db.SaveChangesAsync();
        foreach (var user in users)
        {
            user.CreditBalance -= 35;
            user.HeldCredits += 35;
            db.Enrollments.Add(new Enrollment { UserId = user.Id, CourseId = intake.CourseId,
                CourseSessionId = session.Id, Status = EnrollmentStatus.Reserved, CreditsSpent = 35 });
            session.SeatsTaken++;
        }
        await db.SaveChangesAsync();
        var heldBalances = users.ToDictionary(u => u.Id, u => u.CreditBalance);

        using var settleScope = factory.Services.CreateScope();
        var settlement = settleScope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
        var first = await settlement.SettleIfDueAsync(intake.Id);
        var second = await settlement.SettleIfDueAsync(intake.Id);
        Assert.Equal(reservedCount, first.ProcessedCount);
        Assert.Equal(0, second.ProcessedCount);
        Assert.Equal(runs, first.Confirmed);
        foreach (var user in users)
        {
            var saved = await db.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
            Assert.Equal(0, saved.HeldCredits);
            Assert.Equal(runs ? heldBalances[user.Id] : heldBalances[user.Id] + 35, saved.CreditBalance);
        }
        var status = runs ? EnrollmentStatus.Active : EnrollmentStatus.Cancelled;
        Assert.Equal(reservedCount, await db.Enrollments.CountAsync(e => e.CourseSession.CourseIntakeId == intake.Id && e.Status == status));
        var type = runs ? CreditTransactionType.Capture : CreditTransactionType.Release;
        Assert.Equal(reservedCount, await db.CreditTransactions.CountAsync(t => t.Type == type && t.RelatedEnrollmentId != null));
    }

    [Fact]
    public async Task EnrolmentHoldsCredits_ThenCancellationReleasesThemOnce()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int courseId;
        int sessionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
            var start = DateTime.UtcNow.AddDays(20);
            intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-1);
            intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(5);
            intake.StartsAt = start;
            intake.EndsAt = start.AddDays(1);
            intake.Status = CourseIntakeStatus.Published;
            var session = intake.Sessions.Single();
            session.StartsAt = start;
            session.EndsAt = start.AddHours(2);
            session.PhysicalAddress = null;
            session.PhysicalCapacity = 0;
            session.PhysicalBookingDeadline = null;
            session.MeetingLink = "https://example.com/lesson";
            await db.SaveChangesAsync();
            courseId = intake.CourseId;
            sessionId = session.Id;
        }

        var before = await member.GetFromJsonAsync<JsonObject>("/api/auth/me");
        var available = before!["creditBalance"]!.GetValue<int>();
        var held = before["heldCredits"]?.GetValue<int>() ?? 0;
        var response = await member.PostAsJsonAsync("/api/enrollments", new { courseId, courseSessionId = sessionId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var booked = await response.Content.ReadFromJsonAsync<JsonObject>();
        var id = booked!["enrollmentId"]!.GetValue<int>();
        Assert.Equal(available - booked["creditsSpent"]!.GetValue<int>(), booked["balanceAfter"]!.GetValue<int>());

        var after = await member.GetFromJsonAsync<JsonObject>("/api/auth/me");
        Assert.Equal(held + booked["creditsSpent"]!.GetValue<int>(), after!["heldCredits"]!.GetValue<int>());
        var mine = await member.GetFromJsonAsync<JsonArray>("/api/enrollments/my");
        Assert.Equal("Reserved", mine!.Single(item => item!["id"]!.GetValue<int>() == id)!["status"]!.GetValue<string>());

        var released = await member.PostAsync($"/api/enrollments/{id}/cancel-reservation", null);
        Assert.Equal(HttpStatusCode.OK, released.StatusCode);
        var final = await member.GetFromJsonAsync<JsonObject>("/api/auth/me");
        Assert.Equal(available, final!["creditBalance"]!.GetValue<int>());
        Assert.Equal(held, final["heldCredits"]!.GetValue<int>());
        var ledger = await member.GetFromJsonAsync<JsonArray>("/api/credits/ledger/my");
        Assert.Single(ledger!, item => item!["type"]!.GetValue<string>() == "Hold");
        Assert.Single(ledger!, item => item!["type"]!.GetValue<string>() == "Release");
    }
}
