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

public class EarningsFlowTests
{
    [Fact]
    public async Task Confirmation_creates_no_earnings_but_completion_creates_role_specific_earnings_once()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        var intakeId = await SeedClassAsync(factory, count: 3, cost: 60, minimum: 3);
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            Assert.True((await service.SettleIfDueAsync(intakeId)).Confirmed);
            Assert.Empty(await scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>().Earnings.ToListAsync());
            await CompleteAsync(scope, intakeId);
            await service.ProcessDueAsync();
            await service.ProcessDueAsync();
        }
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var creator = await ApiClient.AsRoleAsync(factory, SeedData.CreatorEmail, "Creator");
        Assert.Equal(45, (await trainer.GetFromJsonAsync<JsonObject>("/api/trainer/earnings"))!["totalCredits"]!.GetValue<int>());
        Assert.Equal(45, (await creator.GetFromJsonAsync<JsonObject>("/api/creator/earnings"))!["totalCredits"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.Forbidden, (await creator.GetAsync("/api/trainer/earnings")).StatusCode);
    }

    [Fact]
    public async Task Creator_completing_a_class_manually_creates_role_specific_earnings_once()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        var intakeId = await SeedClassAsync(factory, count: 3, cost: 60, minimum: 3);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.True((await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(intakeId)).Confirmed);
            await CompleteAsync(scope, intakeId);
            var session = await db.CourseSessions.SingleAsync(s => s.CourseIntakeId == intakeId);
            session.StartsAt = DateTime.UtcNow.AddHours(-2);
            session.EndsAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
            Assert.Empty(await db.Earnings.ToListAsync());
        }
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var creator = await ApiClient.AsRoleAsync(factory, SeedData.CreatorEmail, "Creator");

        (await creator.PostAsync($"/api/creator/intakes/{intakeId}/complete", null)).EnsureSuccessStatusCode();
        (await creator.PostAsync($"/api/creator/intakes/{intakeId}/complete", null)).EnsureSuccessStatusCode();

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().ProcessDueAsync();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(CourseIntakeStatus.Completed, (await db.CourseIntakes.AsNoTracking().SingleAsync(i => i.Id == intakeId)).Status);
            var rows = await db.Earnings.AsNoTracking().Where(x => x.CourseIntakeId == intakeId).ToListAsync();
            Assert.Equal(6, rows.Count);
            Assert.Equal(3, rows.Count(x => x.Role == EarningRole.Trainer));
            Assert.Equal(3, rows.Count(x => x.Role == EarningRole.Creator));
            Assert.All(rows, row => Assert.Equal(EarningStatus.Pending, row.Status));
        }
        Assert.Equal(45, (await trainer.GetFromJsonAsync<JsonObject>("/api/trainer/earnings"))!["totalCredits"]!.GetValue<int>());
        Assert.Equal(45, (await creator.GetFromJsonAsync<JsonObject>("/api/creator/earnings"))!["totalCredits"]!.GetValue<int>());
    }

    [Fact]
    public async Task Failed_class_creates_no_earnings()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        var intakeId = await SeedClassAsync(factory, count: 2, cost: 60, minimum: 3);
        using var scope = factory.Services.CreateScope();
        Assert.False((await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(intakeId)).Confirmed);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>().Earnings.ToListAsync());
    }

    [Fact]
    public async Task Completion_uses_the_course_price_pool_distribution_and_refund_voids_unpaid_rows()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        var intakeId = await SeedClassAsync(factory, count: 3, cost: 60, minimum: 3);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var course = await db.Courses.SingleAsync(c => c.Code == "INFT 4010");
            var subject = await db.CourseSubjects.SingleAsync(s => s.Name == "Digital Product Design");
            course.CourseSubjectId = subject.Id;
            var pool = await db.PricePools.SingleAsync(p => p.Subject == subject.Subject && p.CourseLevelId == course.CourseLevelId);
            pool.ContentSharePercent = 35; pool.DeliverySharePercent = 40; pool.PlatformSharePercent = 25;
            await db.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
            await service.SettleIfDueAsync(intakeId);
            await CompleteAsync(scope, intakeId);
            await service.ProcessDueAsync();
            var enrollment = await db.Enrollments.Where(e => e.CourseSession.CourseIntakeId == intakeId)
                .Select(e => new { e.Id, e.UserId }).FirstAsync();
            await EarningsLedger.VoidRefundableAsync(db, enrollment.Id, "Test refund", default, userId: enrollment.UserId);
            await db.SaveChangesAsync();
        }
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var creator = await ApiClient.AsRoleAsync(factory, SeedData.CreatorEmail, "Creator");
        Assert.Equal(48, (await trainer.GetFromJsonAsync<JsonObject>("/api/trainer/earnings"))!["totalCredits"]!.GetValue<int>());
        Assert.Equal(42, (await creator.GetFromJsonAsync<JsonObject>("/api/creator/earnings"))!["totalCredits"]!.GetValue<int>());
    }

    [Fact]
    public async Task Earnings_schema_upgrade_is_idempotent_for_an_existing_sqlite_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE CourseIntakes (Id INTEGER PRIMARY KEY); CREATE TABLE Courses (Id INTEGER PRIMARY KEY); CREATE TABLE Users (Id INTEGER PRIMARY KEY); CREATE TABLE Enrollments (Id INTEGER PRIMARY KEY); CREATE TABLE AdminAccounts (Id INTEGER PRIMARY KEY);");
        await EarningsPayoutSchema.EnsureAsync(db); await EarningsPayoutSchema.EnsureAsync(db);
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'Earnings'").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'PayoutRequests'").SingleAsync());
    }

    private static async Task CompleteAsync(IServiceScope scope, int intakeId)
    {
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.SingleAsync(i => i.Id == intakeId);
        intake.Status = CourseIntakeStatus.InProgress;
        intake.StartsAt = DateTime.UtcNow.AddHours(-2);
        intake.EndsAt = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
    }

    private static async Task<int> SeedClassAsync(CoLearnXApiFactory factory, int count, int cost, int minimum)
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
        await db.SaveChangesAsync(); return intake.Id;
    }
}
