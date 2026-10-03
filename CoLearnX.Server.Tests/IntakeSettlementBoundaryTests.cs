using System.Net;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class IntakeSettlementBoundaryTests
{
    private const int Cost = 35;
    private const int AvailableAfterHold = 65;

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    public async Task DeadlineSettlesAllSessionsAtTenLearnersExactlyOnce(int count, bool confirmed)
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedReservationsAsync(factory, count);
        using (var scope = factory.Services.CreateScope())
        {
            var settlement = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            var first = await settlement.SettleIfDueAsync(scenario.IntakeId);
            var second = await settlement.SettleIfDueAsync(scenario.IntakeId);
            Assert.Equal(confirmed, first.Confirmed);
            Assert.Equal(count, first.ProcessedCount);
            Assert.Equal(0, second.ProcessedCount);
        }

        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.SingleAsync(i => i.Id == scenario.IntakeId);
        Assert.Equal(confirmed ? CourseIntakeStatus.Published : CourseIntakeStatus.Cancelled, intake.Status);
        Assert.Equal(confirmed, intake.ConfirmedToRunAt.HasValue);
        Assert.Equal(!confirmed, intake.CancelledAt.HasValue);
        var enrollments = await db.Enrollments.Where(e => scenario.EnrollmentIds.Contains(e.Id)).ToListAsync();
        Assert.All(enrollments, e => Assert.Equal(confirmed ? EnrollmentStatus.Active : EnrollmentStatus.Cancelled, e.Status));
        var users = await db.Users.Where(u => scenario.UserIds.Contains(u.Id)).ToListAsync();
        Assert.All(users, u =>
        {
            Assert.Equal(0, u.HeldCredits);
            Assert.Equal(confirmed ? AvailableAfterHold : AvailableAfterHold + Cost, u.CreditBalance);
        });
        var sessions = await db.CourseSessions.Where(s => s.CourseIntakeId == scenario.IntakeId).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s => Assert.Equal(confirmed ? enrollments.Count(e => e.CourseSessionId == s.Id) : 0, s.SeatsTaken));
        var ledger = await db.CreditTransactions.Where(t => t.RelatedEnrollmentId != null
            && scenario.EnrollmentIds.Contains(t.RelatedEnrollmentId.Value)).ToListAsync();
        Assert.Equal(count, ledger.Count);
        Assert.All(enrollments, e => Assert.Single(ledger, t => t.RelatedEnrollmentId == e.Id));
        Assert.All(ledger, t =>
        {
            Assert.Equal(confirmed ? CreditTransactionType.Capture : CreditTransactionType.Release, t.Type);
            Assert.Equal(confirmed ? 0 : Cost, t.Delta);
            Assert.Equal(0, t.HeldAfter);
        });
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    public async Task InconsistentHeldCreditsRollBackTheEntireSettlement(int count)
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedReservationsAsync(factory, count);
        using (var setup = factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            await db.Users.Where(u => u.Id == scenario.UserIds.Last())
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.HeldCredits, Cost - 1));
        }
        var before = await ReadSnapshotAsync(factory, scenario);
        using (var scope = factory.Services.CreateScope())
        {
            var settlement = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => settlement.SettleIfDueAsync(scenario.IntakeId));
        }
        var after = await ReadSnapshotAsync(factory, scenario);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task TrainerCancelsConfirmedClassWithFullRefundExactlyOnce()
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedReservationsAsync(factory, 10);
        using (var scope = factory.Services.CreateScope())
        {
            var settled = await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>()
                .SettleIfDueAsync(scenario.IntakeId);
            Assert.True(settled.Confirmed);
        }
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        var first = await trainer.PostAsync($"/api/trainer/intakes/{scenario.IntakeId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var afterFirst = await ReadSnapshotAsync(factory, scenario);
        var second = await trainer.PostAsync($"/api/trainer/intakes/{scenario.IntakeId}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(afterFirst, await ReadSnapshotAsync(factory, scenario));

        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(CourseIntakeStatus.Cancelled, (await db.CourseIntakes.FindAsync(scenario.IntakeId))!.Status);
        Assert.All(await db.Users.Where(u => scenario.UserIds.Contains(u.Id)).ToListAsync(), u =>
        {
            Assert.Equal(AvailableAfterHold + Cost, u.CreditBalance);
            Assert.Equal(0, u.HeldCredits);
        });
        Assert.All(await db.Enrollments.Where(e => scenario.EnrollmentIds.Contains(e.Id)).ToListAsync(),
            e => Assert.Equal(EnrollmentStatus.Refunded, e.Status));
        Assert.All(await db.CourseSessions.Where(s => s.CourseIntakeId == scenario.IntakeId).ToListAsync(),
            s => Assert.Equal(0, s.SeatsTaken));
        var ledger = await db.CreditTransactions.Where(t => t.RelatedEnrollmentId != null
            && scenario.EnrollmentIds.Contains(t.RelatedEnrollmentId.Value)).ToListAsync();
        Assert.Equal(20, ledger.Count);
        foreach (var enrollmentId in scenario.EnrollmentIds)
        {
            Assert.Single(ledger, t => t.RelatedEnrollmentId == enrollmentId && t.Type == CreditTransactionType.Capture);
            var refund = Assert.Single(ledger, t => t.RelatedEnrollmentId == enrollmentId && t.Type == CreditTransactionType.Refund);
            Assert.Equal(Cost, refund.Delta);
        }
    }

    [Fact]
    public async Task NonOwnerCannotCancelOrMoveCredits()
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedReservationsAsync(factory, 10);
        using (var setup = factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var creatorId = await db.Users.Where(u => u.Email == SeedData.CreatorEmail).Select(u => u.Id).SingleAsync();
            await db.CourseIntakes.Where(i => i.Id == scenario.IntakeId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.TrainerId, creatorId));
        }
        var before = await ReadSnapshotAsync(factory, scenario);
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        var response = await trainer.PostAsync($"/api/trainer/intakes/{scenario.IntakeId}/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, await ReadSnapshotAsync(factory, scenario));
    }

    [Fact]
    public async Task MinimumEnrollmentDoesNotSettleBeforeDeadline()
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedReservationsAsync(factory, 10, due: false);
        var before = await ReadSnapshotAsync(factory, scenario);
        using (var scope = factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>()
                .SettleIfDueAsync(scenario.IntakeId);
            Assert.False(result.Confirmed);
            Assert.Equal(0, result.ProcessedCount);
        }
        Assert.Equal(before, await ReadSnapshotAsync(factory, scenario));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReservationCancellationAndFailedClassSettlementNeverReleaseTwice(bool cancelFirst)
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedReservationsAsync(factory, 9);
        using (var scope = factory.Services.CreateScope())
        {
            var enrollment = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
            var settlement = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            if (cancelFirst)
                await enrollment.CancelReservationAsync(scenario.UserIds[0], scenario.EnrollmentIds[0]);
            var result = await settlement.SettleIfDueAsync(scenario.IntakeId);
            Assert.False(result.Confirmed);
            Assert.Equal(cancelFirst ? 8 : 9, result.ProcessedCount);
            var exception = await Assert.ThrowsAsync<CourseException>(() =>
                enrollment.CancelReservationAsync(scenario.UserIds[0], scenario.EnrollmentIds[0]));
            Assert.Equal("RESERVATION_NOT_CANCELLABLE", exception.Code);
            Assert.Equal(0, (await settlement.SettleIfDueAsync(scenario.IntakeId)).ProcessedCount);
        }
        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.All(await db.Users.Where(u => scenario.UserIds.Contains(u.Id)).ToListAsync(), u =>
        {
            Assert.Equal(AvailableAfterHold + Cost, u.CreditBalance);
            Assert.Equal(0, u.HeldCredits);
        });
        var ledger = await db.CreditTransactions.Where(t => t.RelatedEnrollmentId != null
            && scenario.EnrollmentIds.Contains(t.RelatedEnrollmentId.Value)).ToListAsync();
        Assert.Equal(9, ledger.Count);
        foreach (var id in scenario.EnrollmentIds)
            Assert.Single(ledger, t => t.RelatedEnrollmentId == id && t.Type == CreditTransactionType.Release);
        Assert.All(await db.CourseSessions.Where(s => s.CourseIntakeId == scenario.IntakeId).ToListAsync(),
            s => Assert.Equal(0, s.SeatsTaken));
    }

    [Fact]
    public async Task CapturedReservationCannotBeReleasedByReservationCancellation()
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedReservationsAsync(factory, 10);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(scenario.IntakeId);
        var before = await ReadSnapshotAsync(factory, scenario);
        using (var scope = factory.Services.CreateScope())
        {
            var exception = await Assert.ThrowsAsync<CourseException>(() =>
                scope.ServiceProvider.GetRequiredService<IEnrollmentService>()
                    .CancelReservationAsync(scenario.UserIds[0], scenario.EnrollmentIds[0]));
            Assert.Equal("RESERVATION_NOT_CANCELLABLE", exception.Code);
        }
        Assert.Equal(before, await ReadSnapshotAsync(factory, scenario));
    }

    private static async Task<Scenario> SeedReservationsAsync(CoLearnXApiFactory factory, int count, bool due = true)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Course.Code == "INFT 4010");
        intake.Status = CourseIntakeStatus.Published;
        intake.MinEnrollment = 10;
        intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-3);
        intake.RegistrationClosesAt = due ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddDays(1);
        intake.StartsAt = DateTime.UtcNow.AddDays(20);
        intake.EndsAt = intake.StartsAt.AddDays(2);
        intake.ConfirmedToRunAt = null;
        intake.CancelledAt = null;
        var first = intake.Sessions.Single();
        first.StartsAt = intake.StartsAt;
        first.EndsAt = first.StartsAt.AddHours(2);
        first.SeatsTaken = 0;
        var second = new CourseSession { CourseIntakeId = intake.Id, Label = "Second session",
            StartsAt = intake.StartsAt.AddDays(1), EndsAt = intake.StartsAt.AddDays(1).AddHours(2) };
        db.CourseSessions.Add(second);
        var users = Enumerable.Range(0, count).Select(i => new User
        {
            Email = $"settlement-boundary-{Guid.NewGuid():N}@example.com", FullName = $"Boundary learner {i}",
            DisplayName = $"Learner {i}", PasswordHash = "unused", CreditBalance = AvailableAfterHold, HeldCredits = Cost,
        }).ToList();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        var enrollments = users.Select((user, i) =>
        {
            var session = i % 2 == 0 ? first : second;
            session.SeatsTaken++;
            return new Enrollment { UserId = user.Id, CourseId = intake.CourseId, CourseSessionId = session.Id,
                Status = EnrollmentStatus.Reserved, CreditsSpent = Cost };
        }).ToList();
        db.Enrollments.AddRange(enrollments);
        await db.SaveChangesAsync();
        return new Scenario(intake.Id, users.Select(u => u.Id).ToArray(), enrollments.Select(e => e.Id).ToArray());
    }

    private static async Task<string> ReadSnapshotAsync(CoLearnXApiFactory factory, Scenario scenario)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.AsNoTracking().Where(i => i.Id == scenario.IntakeId)
            .Select(i => new { i.Status, i.ConfirmedToRunAt, i.CancelledAt }).SingleAsync();
        var users = await db.Users.AsNoTracking().Where(u => scenario.UserIds.Contains(u.Id)).OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.CreditBalance, u.HeldCredits }).ToListAsync();
        var enrollments = await db.Enrollments.AsNoTracking().Where(e => scenario.EnrollmentIds.Contains(e.Id)).OrderBy(e => e.Id)
            .Select(e => new { e.Id, e.Status }).ToListAsync();
        var sessions = await db.CourseSessions.AsNoTracking().Where(s => s.CourseIntakeId == scenario.IntakeId).OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.SeatsTaken }).ToListAsync();
        var ledger = await db.CreditTransactions.AsNoTracking().Where(t => t.RelatedEnrollmentId != null
                && scenario.EnrollmentIds.Contains(t.RelatedEnrollmentId.Value)).OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Type, t.Delta, t.BalanceAfter, t.HeldAfter }).ToListAsync();
        return System.Text.Json.JsonSerializer.Serialize(new { intake, users, enrollments, sessions, ledger });
    }

    private sealed record Scenario(int IntakeId, int[] UserIds, int[] EnrollmentIds);
}
