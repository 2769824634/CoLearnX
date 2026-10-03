using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class IntakePostponementTests
{
    [Fact]
    public async Task BookedIntakeCannotReopenItsDeadlineThroughAChangeRequest()
    {
        using var factory = new CoLearnXApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 3030");
        intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-2);
        intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(2);
        intake.StartsAt = DateTime.UtcNow.AddDays(20);
        intake.EndsAt = intake.StartsAt.AddDays(5);
        foreach (var session in intake.Sessions)
        { session.StartsAt = intake.StartsAt; session.EndsAt = intake.StartsAt.AddHours(2); }
        await db.SaveChangesAsync();
        var request = new CreateCourseIntakeChangeRequest(intake.RegistrationOpensAt, intake.RegistrationClosesAt.AddDays(1),
            intake.StartsAt, intake.EndsAt, intake.Sessions.Select(s => new ProposedCourseSessionRequest(s.Id, s.Label,
                s.StartsAt, s.EndsAt, s.MeetingLink, s.PhysicalAddress, s.PhysicalCapacity, s.PhysicalBookingDeadline)).ToList(), intake.Version);
        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => scope.ServiceProvider.GetRequiredService<ICourseIntakeService>()
            .RequestChangeAsync(intake.TrainerId, intake.Id, request));
        Assert.Equal("INTAKE_HAS_COMMITTED_ENROLLMENTS", error.Code);
    }

    [Fact]
    public async Task ConfirmedClassMovesToInProgressWhenDeliveryStarts()
    {
        using var factory = new CoLearnXApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.SingleAsync(i => i.Course.Code == "INFT 3030");
        intake.Status = CourseIntakeStatus.Published;
        intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-20);
        intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(-11);
        intake.StartsAt = DateTime.UtcNow.AddMinutes(-1);
        intake.EndsAt = DateTime.UtcNow.AddDays(1);
        intake.ConfirmedToRunAt = DateTime.UtcNow.AddDays(-10);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().ProcessDueAsync();
        Assert.Equal(CourseIntakeStatus.InProgress, (await db.CourseIntakes.AsNoTracking().SingleAsync(i => i.Id == intake.Id)).Status);
    }

    [Theory]
    [InlineData(7, -1, 201)]
    [InlineData(7, 1, 409)]
    public async Task ReplacementCreationEnforcesSevenDayWindow(int days, int minutes, int expected)
    {
        using var factory = new CoLearnXApiFactory();
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        var (oldId, _) = await FailedClass(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var old = (await db.CourseIntakes.FindAsync(oldId))!;
            old.CancelledAt = DateTime.UtcNow.AddDays(-days).AddMinutes(-minutes);
            await db.SaveChangesAsync();
        }
        Assert.Equal((HttpStatusCode)expected, (await trainer.PostAsJsonAsync($"/api/trainer/intakes/{oldId}/postpone", Schedule())).StatusCode);
    }

    [Theory]
    [InlineData("expired", 409)]
    [InlineData("insufficient", 400)]
    [InlineData("full", 400)]
    [InlineData("other-enrollment", 404)]
    [InlineData("unrelated-session", 409)]
    [InlineData("self-cancelled", 409)]
    public async Task InvalidAcceptanceDoesNotMoveWalletOrSeats(string scenario, int status)
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        var (oldId, enrollmentId) = await FailedClass(factory);
        var response = await trainer.PostAsJsonAsync($"/api/trainer/intakes/{oldId}/postpone", Schedule());
        response.EnsureSuccessStatusCode();
        var draft = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var id = draft["id"]!.GetValue<int>();
        var sessionId = draft["sessions"]![0]!["id"]!.GetValue<int>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var replacement = (await db.CourseIntakes.FindAsync(id))!;
            replacement.Status = CourseIntakeStatus.Published;
            if (scenario == "expired") (await db.CourseIntakes.FindAsync(oldId))!.CancelledAt = DateTime.UtcNow.AddDays(-8);
            if (scenario == "insufficient") (await db.Users.SingleAsync(u => u.Email == SeedData.MemberEmail)).CreditBalance = 0;
            if (scenario == "full") { var s = (await db.CourseSessions.FindAsync(sessionId))!; s.PhysicalCapacity = 10; s.SeatsTaken = 10; }
            if (scenario == "other-enrollment") (await db.Enrollments.FindAsync(enrollmentId))!.UserId = replacement.TrainerId;
            if (scenario == "self-cancelled") (await db.Enrollments.FindAsync(enrollmentId))!.PostponementEligible = false;
            if (scenario == "unrelated-session") replacement.ReplacementForIntakeId = null;
            await db.SaveChangesAsync();
        }
        var before = (await member.GetFromJsonAsync<JsonObject>("/api/auth/me"))!;
        var accepted = await member.PostAsJsonAsync($"/api/enrollments/{enrollmentId}/accept-postponement", new { courseSessionId = sessionId });
        Assert.Equal((HttpStatusCode)status, accepted.StatusCode);
        var after = (await member.GetFromJsonAsync<JsonObject>("/api/auth/me"))!;
        Assert.Equal(before["creditBalance"]!.GetValue<int>(), after["creditBalance"]!.GetValue<int>());
        Assert.Equal(before["heldCredits"]!.GetValue<int>(), after["heldCredits"]!.GetValue<int>());
        using var check = factory.Services.CreateScope();
        var saved = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.False(await saved.Enrollments.AnyAsync(e => e.PostponedFromEnrollmentId == enrollmentId));
        Assert.Equal(scenario == "full" ? 10 : 0, (await saved.CourseSessions.FindAsync(sessionId))!.SeatsTaken);
    }

    [Fact]
    public async Task WrongTrainerAndTrainerCancellationCannotCreateReplacement()
    {
        using var factory = new CoLearnXApiFactory();
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(factory);
        var (oldId, _) = await FailedClass(factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/api/trainer/intakes/{oldId}/postpone", Schedule())).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var old = (await db.CourseIntakes.FindAsync(oldId))!;
            old.CancellationReason = "TrainerCancelled";
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await trainer.PostAsJsonAsync($"/api/trainer/intakes/{oldId}/postpone", Schedule())).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var old = (await db.CourseIntakes.FindAsync(oldId))!;
            old.TrainerId = await db.Users.Where(u => u.Email == SeedData.CreatorEmail).Select(u => u.Id).SingleAsync();
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await trainer.PostAsJsonAsync($"/api/trainer/intakes/{oldId}/postpone", Schedule())).StatusCode);
    }

    [Fact]
    public async Task FailedClassCanBeReplacedReviewedAndReservedOnce()
    {
        using var factory = new CoLearnXApiFactory();
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(factory);
        var (oldId, enrollmentId) = await FailedClass(factory);
        var replacement = await trainer.PostAsJsonAsync($"/api/trainer/intakes/{oldId}/postpone", Schedule());
        Assert.Equal(HttpStatusCode.Created, replacement.StatusCode);
        var draft = (await replacement.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(oldId, draft["replacementForIntakeId"]!.GetValue<int>());
        Assert.Equal("Draft", draft["status"]!.GetValue<string>());
        var id = draft["id"]!.GetValue<int>();
        var sessionId = draft["sessions"]![0]!["id"]!.GetValue<int>();
        Assert.Equal(HttpStatusCode.Conflict, (await trainer.PostAsJsonAsync($"/api/trainer/intakes/{oldId}/postpone", Schedule())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await member.PostAsJsonAsync($"/api/enrollments/{enrollmentId}/accept-postponement", new { courseSessionId = sessionId })).StatusCode);
        var submitted = await trainer.PostAsJsonAsync($"/api/trainer/intakes/{id}/submit", new { version = draft["version"]!.GetValue<string>() });
        submitted.EnsureSuccessStatusCode();
        var pending = (await submitted.Content.ReadFromJsonAsync<JsonObject>())!;
        using var creator = await ApiClient.AsRoleAsync(factory, SeedData.CreatorEmail, "Creator");
        var review = await creator.PostAsJsonAsync($"/api/creator/intake-applications/{id}/review", new { decision = "Confirm", version = pending["version"]!.GetValue<string>() });
        review.EnsureSuccessStatusCode();
        var mine = (await member.GetFromJsonAsync<JsonArray>("/api/enrollments/my"))!;
        var old = mine.Single(e => e!["id"]!.GetValue<int>() == enrollmentId)!;
        Assert.Single(old["postponementOptions"]!.AsArray());
        var before = (await member.GetFromJsonAsync<JsonObject>("/api/auth/me"))!;
        var accepted = await member.PostAsJsonAsync($"/api/enrollments/{enrollmentId}/accept-postponement", new { courseSessionId = sessionId });
        accepted.EnsureSuccessStatusCode();
        var result = (await accepted.Content.ReadFromJsonAsync<JsonObject>())!;
        var newId = result["enrollmentId"]!.GetValue<int>();
        var cost = result["creditsSpent"]!.GetValue<int>();
        Assert.Equal(before["creditBalance"]!.GetValue<int>() - cost, result["balanceAfter"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.Conflict, (await member.PostAsJsonAsync($"/api/enrollments/{enrollmentId}/accept-postponement", new { courseSessionId = sessionId })).StatusCode);
        (await member.PostAsync($"/api/enrollments/{newId}/cancel-reservation", null)).EnsureSuccessStatusCode();
        // Consuming the invitation survives cancellation of the replacement reservation.
        Assert.Equal(HttpStatusCode.Conflict, (await member.PostAsJsonAsync($"/api/enrollments/{enrollmentId}/accept-postponement", new { courseSessionId = sessionId })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(1, await db.CreditTransactions.CountAsync(t => t.RelatedEnrollmentId == newId && t.Type == CreditTransactionType.Hold));
    }

    [Theory]
    [InlineData(5, 409)]
    [InlineData(6, 200)]
    [InlineData(10, 200)]
    [InlineData(11, 409)]
    public async Task WithdrawalUsesUtcCalendarDatesAndOnlySixThroughTenDays(int days, int status)
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var e = await db.Enrollments.Include(e => e.CourseSession).ThenInclude(s => s.CourseIntake)
                .SingleAsync(e => e.Status == EnrollmentStatus.Active && e.Course.Code == "INFT 3030");
            id = e.Id;
            var intake = e.CourseSession.CourseIntake;
            intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-20);
            intake.RegistrationClosesAt = DateTime.UtcNow.AddDays(-10);
            intake.StartsAt = DateTime.UtcNow.Date.AddDays(days).AddMinutes(1);
            intake.EndsAt = intake.StartsAt.AddDays(1);
            intake.ConfirmedToRunAt = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal((HttpStatusCode)status, (await member.PostAsync($"/api/enrollments/{id}/withdraw", null)).StatusCode);
    }

    private static object Schedule() => new {
        registrationOpensAt = DateTime.UtcNow.AddMinutes(-1), registrationClosesAt = DateTime.UtcNow.AddDays(5),
        startsAt = DateTime.UtcNow.AddDays(30), endsAt = DateTime.UtcNow.AddDays(32), minEnrollment = 10,
    };

    private static async Task<(int IntakeId, int EnrollmentId)> FailedClass(CoLearnXApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
        intake.Status = CourseIntakeStatus.Published;
        intake.MinEnrollment = 10;
        intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-5);
        intake.RegistrationClosesAt = DateTime.UtcNow.AddMinutes(-1);
        intake.StartsAt = DateTime.UtcNow.AddDays(15);
        intake.EndsAt = intake.StartsAt.AddDays(1);
        var session = intake.Sessions.Single();
        session.StartsAt = intake.StartsAt;
        session.EndsAt = intake.StartsAt.AddHours(2);
        session.PhysicalCapacity = 0;
        session.PhysicalAddress = null;
        session.PhysicalBookingDeadline = null;
        session.MeetingLink = "https://example.test/class";
        session.SeatsTaken = 1;
        var user = await db.Users.SingleAsync(u => u.Email == SeedData.MemberEmail);
        user.CreditBalance -= 35;
        user.HeldCredits += 35;
        var enrollment = new Enrollment { UserId = user.Id, CourseId = intake.CourseId, CourseSessionId = session.Id,
            Status = EnrollmentStatus.Reserved, CreditsSpent = 35 };
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(intake.Id);
        return (intake.Id, enrollment.Id);
    }
}
