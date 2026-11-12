using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

namespace CoLearnX.Server.Tests;

public class EarningsFlowTests
{
    [Fact]
    public async Task Confirmed_class_creates_role_specific_earnings_once_and_cancellation_reverses_them()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        var (intakeId, creatorId) = await SeedClassAsync(factory, count: 3, cost: 60, minimum: 3);
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            Assert.True((await service.SettleIfDueAsync(intakeId)).Confirmed);
            Assert.Equal(0, (await service.SettleIfDueAsync(intakeId)).ProcessedCount);
        }
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var creator = await ApiClient.AsRoleAsync(factory, SeedData.CreatorEmail, "Creator");
        var trainerSummary = await trainer.GetFromJsonAsync<JsonObject>("/api/trainer/earnings");
        var creatorSummary = await creator.GetFromJsonAsync<JsonObject>("/api/creator/earnings");
        Assert.Equal(45, trainerSummary!["totalCredits"]!.GetValue<int>());
        Assert.Equal(45, creatorSummary!["totalCredits"]!.GetValue<int>());
        Assert.Equal(1, trainerSummary["confirmedClasses"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.Forbidden, (await creator.GetAsync("/api/trainer/earnings")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            await service.CancelIntakeAsync(creatorId, intakeId);
            await service.CancelIntakeAsync(creatorId, intakeId);
        }
        Assert.Equal(0, (await trainer.GetFromJsonAsync<JsonObject>("/api/trainer/earnings"))!["totalCredits"]!.GetValue<int>());
        Assert.Equal(0, (await creator.GetFromJsonAsync<JsonObject>("/api/creator/earnings"))!["totalCredits"]!.GetValue<int>());
    }

    [Fact]
    public async Task Failed_class_creates_no_earnings()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        var (intakeId, _) = await SeedClassAsync(factory, count: 2, cost: 60, minimum: 3);
        using (var scope = factory.Services.CreateScope())
            Assert.False((await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(intakeId)).Confirmed);
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        var summary = await trainer.GetFromJsonAsync<JsonObject>("/api/trainer/earnings");
        Assert.Equal(0, summary!["totalCredits"]!.GetValue<int>());
        Assert.Empty(summary["rows"]!.AsArray());
    }

    [Fact]
    public async Task Partial_withdrawal_reverses_only_the_refunded_share()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        var (intakeId, _) = await SeedClassAsync(factory, count: 3, cost: 60, minimum: 3);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(intakeId);
            var intake = await db.CourseIntakes.SingleAsync(i => i.Id == intakeId);
            intake.StartsAt = DateTime.UtcNow.AddDays(8);
            await db.SaveChangesAsync();
            var enrollment = await db.Enrollments.Include(e => e.CourseSession)
                .FirstAsync(e => e.CourseSession.CourseIntakeId == intakeId);
            await scope.ServiceProvider.GetRequiredService<IEnrollmentService>().WithdrawAsync(enrollment.UserId, enrollment.Id);
        }
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var creator = await ApiClient.AsRoleAsync(factory, SeedData.CreatorEmail, "Creator");
        Assert.Equal(35, (await trainer.GetFromJsonAsync<JsonObject>("/api/trainer/earnings"))!["totalCredits"]!.GetValue<int>());
        Assert.Equal(35, (await creator.GetFromJsonAsync<JsonObject>("/api/creator/earnings"))!["totalCredits"]!.GetValue<int>());
    }

    [Fact]
    public async Task Confirmed_class_uses_its_existing_price_pool_distribution()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        var (intakeId, _) = await SeedClassAsync(factory, count: 3, cost: 60, minimum: 3);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var course = await db.Courses.SingleAsync(c => c.Code == "INFT 4010");
            var subject = await db.CourseSubjects.SingleAsync(s => s.Name == "Digital Product Design");
            course.CourseSubjectId = subject.Id;
            var pool = await db.PricePools.SingleAsync(p => p.Subject == subject.Subject && p.CourseLevelId == course.CourseLevelId);
            pool.ContentSharePercent = 35;
            pool.DeliverySharePercent = 40;
            pool.PlatformSharePercent = 25;
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(intakeId);
        }
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var creator = await ApiClient.AsRoleAsync(factory, SeedData.CreatorEmail, "Creator");
        Assert.Equal(72, (await trainer.GetFromJsonAsync<JsonObject>("/api/trainer/earnings"))!["totalCredits"]!.GetValue<int>());
        Assert.Equal(63, (await creator.GetFromJsonAsync<JsonObject>("/api/creator/earnings"))!["totalCredits"]!.GetValue<int>());
    }

    [Fact]
    public async Task Full_refunds_reverse_class_rounding_without_residual_credits()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        var (intakeId, creatorId) = await SeedClassAsync(factory, count: 2, cost: 1, minimum: 2);
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            await service.SettleIfDueAsync(intakeId);
            await service.CancelIntakeAsync(creatorId, intakeId);
        }
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        var summary = await trainer.GetFromJsonAsync<JsonObject>("/api/trainer/earnings");
        Assert.Equal(0, summary!["totalCredits"]!.GetValue<int>());
    }

    [Fact]
    public async Task Earnings_table_upgrade_is_idempotent_for_an_existing_sqlite_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE CourseIntakes (Id INTEGER PRIMARY KEY); CREATE TABLE Users (Id INTEGER PRIMARY KEY); CREATE TABLE Enrollments (Id INTEGER PRIMARY KEY);");
        await IntakeEarningSchema.EnsureAsync(db);
        await IntakeEarningSchema.EnsureAsync(db);
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'IntakeEarnings'").SingleAsync());
    }

    private static async Task<(int IntakeId, int CreatorId)> SeedClassAsync(CoLearnXApiFactory factory, int count, int cost, int minimum)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var course = await db.Courses.SingleAsync(c => c.Code == "INFT 4010");
        var trainer = await db.Users.SingleAsync(u => u.Email == SeedData.TrainerEmail);
        var intake = new CourseIntake { CourseId = course.Id, TrainerId = trainer.Id, Status = CourseIntakeStatus.Published,
            MinEnrollment = minimum, RegistrationOpensAt = DateTime.UtcNow.AddDays(-3), RegistrationClosesAt = DateTime.UtcNow.AddDays(-1),
            StartsAt = DateTime.UtcNow.AddDays(20), EndsAt = DateTime.UtcNow.AddDays(21) };
        var session = new CourseSession { CourseIntake = intake, Label = "Session", StartsAt = intake.StartsAt,
            EndsAt = intake.StartsAt.AddHours(1), MeetingLink = "https://example.com/meeting", SeatsTaken = count };
        db.CourseSessions.Add(session);
        for (var i = 0; i < count; i++)
        {
            var user = new User { Email = $"earn-{Guid.NewGuid():N}@example.test", FullName = "Learner", DisplayName = "Learner",
                PasswordHash = "unused", HeldCredits = cost };
            db.Enrollments.Add(new Enrollment { User = user, CourseId = course.Id, CourseSession = session,
                Status = EnrollmentStatus.Reserved, CreditsSpent = cost });
        }
        await db.SaveChangesAsync();
        return (intake.Id, course.CreatorId);
    }
}
