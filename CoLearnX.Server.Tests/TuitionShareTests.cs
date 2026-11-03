using CoLearnX.Server.Data;
using CoLearnX.Server.Domain;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class TuitionShareTests
{
    [Fact]
    public async Task ConfirmedTuitionPaysCreatorTrainerAndPlatformFromThePriceCell()
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedAsync(factory, cost: 20, learners: 2, contentPercent: 40, deliveryPercent: 10);
        await SettleAsync(factory, scenario.IntakeId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(scenario.CreatorBalance + 16, await BalanceAsync(db, scenario.CreatorId));
        Assert.Equal(scenario.TrainerBalance + 4, await BalanceAsync(db, scenario.TrainerId));
        Assert.Equal(scenario.PlatformBalance + 20, await BalanceAsync(db, scenario.PlatformId));
        var royalties = await db.CreditTransactions.Where(item => item.RelatedEnrollmentId == scenario.EnrollmentId
            && item.Type == CreditTransactionType.Royalty).ToListAsync();
        Assert.Equal(20, royalties.Sum(item => item.Delta));
        Assert.Contains(royalties, item => item.UserId == scenario.CreatorId && item.Description.StartsWith("Content share"));
        Assert.Contains(royalties, item => item.UserId == scenario.TrainerId && item.Description.StartsWith("Delivery share"));
        Assert.Contains(royalties, item => item.UserId == scenario.PlatformId && item.Description.StartsWith("Platform share"));
        Assert.Equal(2, await db.Notifications.CountAsync(item => item.Code == "N-share-recorded" && !item.EmailPending));
    }

    [Fact]
    public async Task StaffTrainerDeliveryStaysWithThePlatform()
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedAsync(factory, cost: 20, learners: 2, contentPercent: 25, deliveryPercent: 25, staffTrainer: true);
        await SettleAsync(factory, scenario.IntakeId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(scenario.CreatorBalance + 10, await BalanceAsync(db, scenario.CreatorId));
        Assert.Equal(scenario.TrainerBalance, await BalanceAsync(db, scenario.TrainerId));
        Assert.Equal(scenario.PlatformBalance + 30, await BalanceAsync(db, scenario.PlatformId));
        Assert.Contains(await db.CreditTransactions.Where(item => item.UserId == scenario.PlatformId).ToListAsync(),
            item => item.Description.StartsWith("Delivery share retained"));
        Assert.Equal(0, await db.Notifications.CountAsync(item => item.UserId == scenario.PlatformId && item.Code == "N-share-recorded"));
        Assert.Equal(0, await db.Notifications.CountAsync(item => item.UserId == scenario.TrainerId && item.Code == "N-share-recorded"));
    }

    [Fact]
    public async Task WithdrawalReturnsSeventyPercentOfEachShare()
    {
        using var factory = new CoLearnXApiFactory();
        var scenario = await SeedAsync(factory, cost: 35, learners: 2, contentPercent: 25, deliveryPercent: 25);
        await SettleAsync(factory, scenario.IntakeId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var starts = DateTime.UtcNow.Date.AddDays(7);
            await db.CourseIntakes.Where(item => item.Id == scenario.IntakeId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.StartsAt, starts));
            await scope.ServiceProvider.GetRequiredService<IEnrollmentService>()
                .WithdrawAsync(scenario.LearnerId, scenario.EnrollmentId);
        }

        using var check = factory.Services.CreateScope();
        var saved = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(scenario.CreatorBalance + 10, await BalanceAsync(saved, scenario.CreatorId));
        Assert.Equal(scenario.TrainerBalance + 10, await BalanceAsync(saved, scenario.TrainerId));
        Assert.Equal(scenario.PlatformBalance + 25, await BalanceAsync(saved, scenario.PlatformId));
        Assert.Equal(10, await saved.CreditTransactions.Where(item => item.RelatedEnrollmentId == scenario.EnrollmentId
            && item.Type == CreditTransactionType.Royalty).SumAsync(item => item.Delta));
    }

    private static async Task SettleAsync(CoLearnXApiFactory factory, int intakeId)
    {
        using var scope = factory.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(intakeId);
        Assert.True(result.Confirmed);
    }

    private static async Task<int> BalanceAsync(CoLearnXDbContext db, int userId)
        => await db.Users.Where(item => item.Id == userId).Select(item => item.CreditBalance).SingleAsync();

    private static async Task<Scenario> SeedAsync(CoLearnXApiFactory factory, int cost, int learners, int contentPercent, int deliveryPercent, bool staffTrainer = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.Include(item => item.Sessions).SingleAsync(item => item.Course.Code == "INFT 4010");
        var course = await db.Courses.SingleAsync(item => item.Id == intake.CourseId);
        var subject = await db.CourseSubjects.SingleAsync(item => item.Name == "Languages");
        var level = await db.CourseLevels.SingleAsync(item => item.Name == "Beginner");
        course.CourseSubjectId = subject.Id;
        course.CourseLevelId = level.Id;
        var cell = await db.PricePools.SingleAsync(item => item.Subject == subject.Subject && item.CourseLevelId == level.Id);
        cell.ContentSharePercent = contentPercent;
        cell.DeliverySharePercent = deliveryPercent;
        cell.PlatformSharePercent = 100 - contentPercent - deliveryPercent;
        intake.Status = CourseIntakeStatus.Published;
        intake.MinEnrollment = Math.Max(2, learners);
        intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-2);
        intake.RegistrationClosesAt = DateTime.UtcNow.AddMinutes(-1);
        intake.StartsAt = DateTime.UtcNow.AddDays(20);
        intake.EndsAt = intake.StartsAt.AddDays(1);
        intake.ConfirmedToRunAt = null;
        intake.CancelledAt = null;
        var session = intake.Sessions.Single();
        session.StartsAt = intake.StartsAt;
        session.EndsAt = intake.StartsAt.AddHours(2);
        session.SeatsTaken = learners;
        if (staffTrainer)
        {
            var adminId = await db.AdminAccounts.Select(item => item.Id).SingleAsync();
            db.StaffAccounts.Add(new StaffAccount
            {
                Email = "share-staff@example.test",
                PasswordHash = "unused",
                FullName = "Staff trainer",
                DisplayName = "Staff trainer",
                StaffNumber = 9001,
                DocumentPath = "staff/share.pdf",
                UserId = intake.TrainerId,
                CreatedByAdminAccountId = adminId,
                SessionStamp = Guid.NewGuid(),
            });
        }
        var created = Enumerable.Range(0, learners).Select(index => new User
        {
            Email = $"share-learner-{index}-{Guid.NewGuid():N}@example.test",
            FullName = $"Share learner {index}",
            DisplayName = $"Share learner {index}",
            PasswordHash = "unused",
            CreditBalance = 100,
            HeldCredits = cost,
        }).ToList();
        db.Users.AddRange(created);
        await db.SaveChangesAsync();
        var enrollment = new Enrollment
        {
            UserId = created[0].Id,
            CourseId = course.Id,
            CourseSessionId = session.Id,
            Status = EnrollmentStatus.Reserved,
            CreditsSpent = cost,
        };
        db.Enrollments.Add(enrollment);
        db.Enrollments.AddRange(created.Skip(1).Select(user => new Enrollment
        {
            UserId = user.Id,
            CourseId = course.Id,
            CourseSessionId = session.Id,
            Status = EnrollmentStatus.Reserved,
            CreditsSpent = cost,
        }));
        await db.SaveChangesAsync();
        var learner = created[0];
        var platformId = await db.Users.Where(item => item.Email == PlatformIdentity.Email).Select(item => item.Id).SingleAsync();
        return new Scenario(intake.Id, enrollment.Id, learner.Id, course.CreatorId, intake.TrainerId, platformId,
            await BalanceAsync(db, course.CreatorId), await BalanceAsync(db, intake.TrainerId), await BalanceAsync(db, platformId));
    }

    private sealed record Scenario(int IntakeId, int EnrollmentId, int LearnerId, int CreatorId, int TrainerId, int PlatformId,
        int CreatorBalance, int TrainerBalance, int PlatformBalance);
}
