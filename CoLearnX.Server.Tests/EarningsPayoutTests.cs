using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class EarningsPayoutTests
{
    [Fact]
    public async Task Member_cannot_request_payout_and_trainer_cannot_read_admin_queue()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        using var member = await ApiClient.AsRoleAsync(factory, SeedData.MemberEmail, "Member");
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PostAsJsonAsync("/api/earnings/payouts", new { payoutMethod = "PayPal" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/earnings/my")).StatusCode);
        Assert.Contains((await trainer.GetAsync("/api/admin/payouts?status=Requested")).StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    [Fact]
    public async Task Completing_an_intake_creates_pending_creator_and_trainer_earnings_once()
    {
        using var factory = new CoLearnXApiFactory();
        using var client = factory.CreateClient();
        var intakeId = await SeedCompletedClassCandidateAsync(factory, learners: 2, credits: 100);

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
        await service.ProcessDueAsync();
        await service.ProcessDueAsync();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var rows = await db.Earnings.Where(x => x.CourseIntakeId == intakeId).ToListAsync();

        Assert.Equal(4, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(EarningStatus.Pending, row.Status);
            Assert.Equal(25, row.SharePercent);
            Assert.Equal(25, row.Credits);
            Assert.True(row.AvailableAt > row.CreatedAt);
        });
        Assert.Equal(CourseIntakeStatus.Completed,
            await db.CourseIntakes.Where(x => x.Id == intakeId).Select(x => x.Status).SingleAsync());

        db.ChangeTracker.Clear();
        await db.Earnings.Where(x => x.CourseIntakeId == intakeId)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.AvailableAt, DateTime.UtcNow.AddMinutes(-1)));
        await service.ProcessDueAsync();
        Assert.All(await db.Earnings.Where(x => x.CourseIntakeId == intakeId).ToListAsync(),
            row => Assert.Equal(EarningStatus.Available, row.Status));
    }

    [Fact]
    public async Task Payout_requires_available_minimum_and_review_is_idempotent()
    {
        using var factory = new CoLearnXApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var trainer = await db.Users.SingleAsync(x => x.Email == SeedData.TrainerEmail);
        var course = await db.Courses.FirstAsync(x => x.TrainerId == trainer.Id);
        var intake = await db.CourseIntakes.FirstAsync(x => x.CourseId == course.Id);
        var enrollment = await db.Enrollments.FirstAsync();
        db.Earnings.Add(new Earning
        {
            UserId = trainer.Id, CourseId = course.Id, CourseIntakeId = intake.Id,
            EnrollmentId = enrollment.Id, Role = EarningRole.Trainer, Credits = 60,
            SharePercent = 25, Status = EarningStatus.Available,
            AvailableAt = DateTime.UtcNow.AddMinutes(-1), CreatedAt = DateTime.UtcNow.AddDays(-8)
        });
        await db.SaveChangesAsync();

        var payouts = scope.ServiceProvider.GetRequiredService<IPayoutService>();
        var request = await payouts.RequestAsync(trainer.Id, "PayPal trainer@example.test");
        Assert.Equal(60, request.Credits);
        var duplicate = await Assert.ThrowsAsync<LaterPhaseException>(() => payouts.RequestAsync(trainer.Id, "Bank"));
        Assert.Equal("PAYOUT_ALREADY_REQUESTED", duplicate.Code);
        var admin = await db.AdminAccounts.FirstAsync();
        var missingReference = await Assert.ThrowsAsync<LaterPhaseException>(() =>
            payouts.ReviewAsync(admin.Id, request.Id, PayoutStatus.Paid, " "));
        Assert.Equal("PAYOUT_REFERENCE_REQUIRED", missingReference.Code);
        var paid = await payouts.ReviewAsync(admin.Id, request.Id, PayoutStatus.Paid, "OFFLINE-001");
        var repeated = await payouts.ReviewAsync(admin.Id, request.Id, PayoutStatus.Paid, "OFFLINE-001");

        Assert.Equal(PayoutStatus.Paid.ToString(), paid.Status);
        Assert.Equal(paid, repeated);
        Assert.Equal(EarningStatus.Paid,
            await db.Earnings.Where(x => x.PayoutRequestId == request.Id).Select(x => x.Status).SingleAsync());
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "PayoutPaid" && x.EntityId == request.Id.ToString()));
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == trainer.Id && x.Code == "N-payout-paid"));
    }

    [Fact]
    public async Task Payout_rejects_below_minimum_excludes_pending_and_releases_rows_after_rejection()
    {
        using var factory = new CoLearnXApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var trainer = await db.Users.SingleAsync(x => x.Email == SeedData.TrainerEmail);
        var course = await db.Courses.FirstAsync(x => x.TrainerId == trainer.Id);
        var intake = await db.CourseIntakes.FirstAsync(x => x.CourseId == course.Id);
        var enrollments = await db.Enrollments.Take(2).ToListAsync();
        db.Earnings.Add(new Earning { UserId = trainer.Id, CourseId = course.Id, CourseIntakeId = intake.Id,
            EnrollmentId = enrollments[0].Id, Role = EarningRole.Trainer, Credits = 49, SharePercent = 25,
            Status = EarningStatus.Available, CreatedAt = DateTime.UtcNow.AddDays(-8), AvailableAt = DateTime.UtcNow.AddDays(-1) });
        db.Earnings.Add(new Earning { UserId = trainer.Id, CourseId = course.Id, CourseIntakeId = intake.Id,
            EnrollmentId = enrollments[1].Id, Role = EarningRole.Trainer, Credits = 70, SharePercent = 25,
            Status = EarningStatus.Pending, CreatedAt = DateTime.UtcNow, AvailableAt = DateTime.UtcNow.AddDays(7) });
        await db.SaveChangesAsync();
        var payouts = scope.ServiceProvider.GetRequiredService<IPayoutService>();
        var below = await Assert.ThrowsAsync<LaterPhaseException>(() => payouts.RequestAsync(trainer.Id, "PayPal"));
        Assert.Equal("PAYOUT_BELOW_MINIMUM", below.Code);
        (await db.Earnings.SingleAsync(x => x.EnrollmentId == enrollments[0].Id)).Credits = 50;
        await db.SaveChangesAsync();
        var request = await payouts.RequestAsync(trainer.Id, "PayPal");
        Assert.Equal(50, request.Credits);
        Assert.Equal(EarningStatus.Pending, await db.Earnings.Where(x => x.EnrollmentId == enrollments[1].Id)
            .Select(x => x.Status).SingleAsync());
        var admin = await db.AdminAccounts.FirstAsync();
        await payouts.ReviewAsync(admin.Id, request.Id, PayoutStatus.Rejected, "Wrong account details");
        Assert.Equal(EarningStatus.Available, await db.Earnings.Where(x => x.EnrollmentId == enrollments[0].Id)
            .Select(x => x.Status).SingleAsync());
        Assert.Equal(50, (await payouts.RequestAsync(trainer.Id, "Bank transfer")).Credits);
    }

    [Fact]
    public async Task Admin_pending_count_tracks_requested_payouts()
    {
        using var factory = new CoLearnXApiFactory();
        using var initializer = factory.CreateClient();
        int payoutId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var trainer = await db.Users.SingleAsync(x => x.Email == SeedData.TrainerEmail);
            var course = await db.Courses.FirstAsync(x => x.TrainerId == trainer.Id);
            var intake = await db.CourseIntakes.FirstAsync(x => x.CourseId == course.Id);
            var enrollment = await db.Enrollments.FirstAsync();
            var payout = new PayoutRequest { UserId = trainer.Id, Credits = 50, PayoutMethod = "PayPal",
                Status = PayoutStatus.Requested };
            db.PayoutRequests.Add(payout);
            db.Earnings.Add(new Earning { UserId = trainer.Id, CourseId = course.Id, CourseIntakeId = intake.Id,
                EnrollmentId = enrollment.Id, Role = EarningRole.Trainer, Credits = 50, SharePercent = 25,
                Status = EarningStatus.Requested, CreatedAt = DateTime.UtcNow.AddDays(-8),
                AvailableAt = DateTime.UtcNow.AddDays(-1), PayoutRequest = payout });
            await db.SaveChangesAsync(); payoutId = payout.Id;
        }
        using var admin = AdminClient(factory);
        Assert.Equal(1, (await admin.GetFromJsonAsync<AdminPendingCountsDto>("/api/admin/pending-counts"))!.Payouts);
        var response = await admin.PostAsJsonAsync($"/api/admin/payouts/{payoutId}/review",
            new { decision = "Paid", reference = "OFF-COUNT" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, (await admin.GetFromJsonAsync<AdminPendingCountsDto>("/api/admin/pending-counts"))!.Payouts);
    }

    private static HttpClient AdminClient(CoLearnXApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var account = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>().AdminAccounts.AsNoTracking()
            .Single(x => x.Email == SeedData.AdminEmail);
        var token = scope.ServiceProvider.GetRequiredService<IAdminTokenService>().CreateToken(account).Token;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<int> SeedCompletedClassCandidateAsync(CoLearnXApiFactory factory, int learners, int credits)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var course = await db.Courses.SingleAsync(x => x.Code == "INFT 4010");
        var trainer = await db.Users.SingleAsync(x => x.Email == SeedData.TrainerEmail);
        var intake = new CourseIntake
        {
            CourseId = course.Id, TrainerId = trainer.Id, Status = CourseIntakeStatus.InProgress,
            MinEnrollment = 2, RegistrationOpensAt = DateTime.UtcNow.AddDays(-20),
            RegistrationClosesAt = DateTime.UtcNow.AddDays(-15), StartsAt = DateTime.UtcNow.AddDays(-2),
            EndsAt = DateTime.UtcNow.AddMinutes(-1), ConfirmedToRunAt = DateTime.UtcNow.AddDays(-15)
        };
        var session = new CourseSession
        {
            CourseIntake = intake, Label = "Session", StartsAt = intake.StartsAt,
            EndsAt = intake.EndsAt, MeetingLink = "https://example.test/class"
        };
        db.CourseSessions.Add(session);
        for (var index = 0; index < learners; index++)
        {
            var learner = new User
            {
                Email = $"payout-{Guid.NewGuid():N}@example.test", FullName = "Learner",
                DisplayName = "Learner", PasswordHash = "unused"
            };
            db.Enrollments.Add(new Enrollment
            {
                User = learner, CourseId = course.Id, CourseSession = session,
                Status = EnrollmentStatus.Active, CreditsSpent = credits
            });
        }
        db.Enrollments.Add(new Enrollment
        {
            User = new User { Email = $"cancelled-{Guid.NewGuid():N}@example.test", FullName = "Cancelled",
                DisplayName = "Cancelled", PasswordHash = "unused" },
            CourseId = course.Id, CourseSession = session, Status = EnrollmentStatus.Cancelled, CreditsSpent = credits
        });
        await db.SaveChangesAsync();
        return intake.Id;
    }
}
