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

public class EnrollmentNotificationLinkTests
{
    [Fact]
    public async Task Reservation_and_cancellation_notifications_keep_the_member_enrollment_id()
    {
        using var factory = new CoLearnXApiFactory();
        var intakeId = await IntakeNotificationTests.SeedDueReservation(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var enrollment = await db.Enrollments.Include(item => item.User)
            .Where(item => item.CourseSession.CourseIntakeId == intakeId && item.Status == Domain.Enums.EnrollmentStatus.Reserved)
            .SingleAsync();
        await scope.ServiceProvider.GetRequiredService<IEnrollmentService>()
            .CancelReservationAsync(enrollment.UserId, enrollment.Id);
        using var member = await ApiClient.AsMemberAsync(factory);

        var inbox = await member.GetFromJsonAsync<JsonObject>("/api/notifications/my");
        var released = inbox!["items"]!.AsArray().Single(item => item!["type"]!.GetValue<string>() == "N-hold-released");
        Assert.Equal(enrollment.Id, released!["enrollmentId"]!.GetValue<int>());
        Assert.Equal("/member/payment", released["targetPath"]!.GetValue<string>());
    }

    [Fact]
    public async Task Legacy_notification_falls_back_to_latest_owned_enrollment_for_its_trusted_intake()
    {
        using var factory = new CoLearnXApiFactory();
        var intakeId = await IntakeNotificationTests.SeedDueReservation(factory);
        int memberId;
        int enrollmentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            memberId = await db.Users.Where(item => item.Email == SeedData.MemberEmail).Select(item => item.Id).SingleAsync();
            enrollmentId = await db.Enrollments.Where(item => item.CourseSession.CourseIntakeId == intakeId).Select(item => item.Id).SingleAsync();
            db.Notifications.Add(new Notification
            {
                UserId = memberId,
                IntakeId = intakeId,
                Code = "N-01",
                Title = "Legacy reservation",
                Body = "Legacy event without a direct enrollment link.",
            });
            await db.SaveChangesAsync();
        }

        using var member = await ApiClient.AsMemberAsync(factory);
        var inbox = await member.GetFromJsonAsync<JsonObject>("/api/notifications/my");
        var legacy = inbox!["items"]!.AsArray().Single(item => item!["title"]!.GetValue<string>() == "Legacy reservation");
        Assert.Equal(enrollmentId, legacy!["enrollmentId"]!.GetValue<int>());
        Assert.Equal($"/member/programs?tab=reserved&enrollmentId={enrollmentId}", legacy["targetPath"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_notification_cannot_expose_a_foreign_enrollment_id()
    {
        using var factory = new CoLearnXApiFactory();
        var intakeId = await IntakeNotificationTests.SeedDueReservation(factory);
        int memberId;
        int ownEnrollmentId;
        int foreignEnrollmentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            memberId = await db.Users.Where(item => item.Email == SeedData.MemberEmail).Select(item => item.Id).SingleAsync();
            ownEnrollmentId = await db.Enrollments.Where(item => item.CourseSession.CourseIntakeId == intakeId).Select(item => item.Id).SingleAsync();
            var foreign = new User { Email = $"foreign-notice-{Guid.NewGuid():N}@example.test", FullName = "Foreign Learner" };
            db.Users.Add(foreign);
            await db.SaveChangesAsync();
            var session = await db.CourseSessions.Where(item => item.CourseIntakeId == intakeId)
                .Select(item => new { item.Id, item.CourseIntake.CourseId }).SingleAsync();
            var enrollment = new Enrollment { UserId = foreign.Id, CourseId = session.CourseId, CourseSessionId = session.Id, CreditsSpent = 35 };
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();
            foreignEnrollmentId = enrollment.Id;
            db.Notifications.Add(new Notification
            {
                UserId = memberId,
                IntakeId = intakeId,
                Code = "N-01",
                Title = "Mismatched reservation",
                Body = "A legacy row contains an unrelated enrollment reference.",
            });
            await db.SaveChangesAsync();
            var notificationId = await db.Notifications.Where(item => item.Title == "Mismatched reservation")
                .Select(item => item.Id).SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Notifications SET EnrollmentId = {foreignEnrollmentId} WHERE Id = {notificationId}");
        }

        using var member = await ApiClient.AsMemberAsync(factory);
        var inbox = await member.GetFromJsonAsync<JsonObject>("/api/notifications/my");
        var mismatched = inbox!["items"]!.AsArray().Single(item => item!["title"]!.GetValue<string>() == "Mismatched reservation");
        Assert.Equal(ownEnrollmentId, mismatched!["enrollmentId"]!.GetValue<int>());
        Assert.NotEqual(foreignEnrollmentId, mismatched["enrollmentId"]!.GetValue<int>());
    }

    [Fact]
    public async Task One_credit_reservation_notifications_use_singular_grammar()
    {
        using var factory = new CoLearnXApiFactory();
        int memberId;
        int courseId;
        int sessionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes
                .Include(item => item.Course)
                .Include(item => item.Sessions)
                .SingleAsync(item => item.Course.Code == "INFT 4010");
            var member = await db.Users.SingleAsync(item => item.Email == SeedData.MemberEmail);
            var now = DateTime.UtcNow;

            intake.Status = CourseIntakeStatus.Published;
            intake.RegistrationOpensAt = now.AddHours(-1);
            intake.RegistrationClosesAt = now.AddHours(1);
            intake.StartsAt = now.AddDays(20);
            intake.EndsAt = now.AddDays(21);
            intake.CancelledAt = null;
            intake.ConfirmedToRunAt = null;
            intake.Course.CreditCost = 1;
            member.CreditBalance = 10;
            member.HeldCredits = 0;
            var session = intake.Sessions.Single();
            session.StartsAt = intake.StartsAt;
            session.EndsAt = intake.StartsAt.AddHours(2);
            session.PhysicalCapacity = 0;
            session.SeatsTaken = 0;
            session.PhysicalBookingDeadline = intake.RegistrationClosesAt;
            await db.SaveChangesAsync();

            memberId = member.Id;
            courseId = intake.CourseId;
            sessionId = session.Id;
        }

        using var scopeAfterSetup = factory.Services.CreateScope();
        var service = scopeAfterSetup.ServiceProvider.GetRequiredService<IEnrollmentService>();
        var reserved = await service.EnrolAsync(memberId, new EnrolRequest(courseId, sessionId));
        var dbAfterReserve = scopeAfterSetup.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var reservationNotice = await dbAfterReserve.Notifications.AsNoTracking()
            .SingleAsync(item => item.EnrollmentId == reserved.EnrollmentId && item.Code == "N-01");

        Assert.Contains("1 credit is on hold", reservationNotice.Body);
        Assert.DoesNotContain("1 credits", reservationNotice.Body);
        Assert.DoesNotContain("credits are", reservationNotice.Body);

        await service.CancelReservationAsync(memberId, reserved.EnrollmentId);
        var releaseNotice = await dbAfterReserve.Notifications.AsNoTracking()
            .Where(item => item.EnrollmentId == reserved.EnrollmentId && item.Code == "N-hold-released")
            .OrderByDescending(item => item.Id)
            .FirstAsync();

        Assert.Contains("1 credit is available again", releaseNotice.Body);
        Assert.DoesNotContain("1 credits", releaseNotice.Body);
        Assert.DoesNotContain("credits are", releaseNotice.Body);
    }

    [Fact]
    public async Task Postponement_notification_keeps_the_original_cancelled_enrollment_id()
    {
        using var factory = new CoLearnXApiFactory();
        var seed = await SeedReplacementAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            db.Notifications.Add(new Notification
            {
                UserId = seed.MemberId,
                IntakeId = seed.ReplacementIntakeId,
                EnrollmentId = seed.OriginalEnrollmentId,
                Code = "N-postponement-offered",
                Title = "Postponed class available",
                Body = "Choose a replacement session.",
            });
            await db.SaveChangesAsync();
        }

        using var member = await ApiClient.AsMemberAsync(factory);
        var inbox = await member.GetFromJsonAsync<JsonObject>("/api/notifications/my");
        var notice = inbox!["items"]!.AsArray().Single(item => item!["title"]!.GetValue<string>() == "Postponed class available");
        Assert.Equal(seed.OriginalEnrollmentId, notice!["enrollmentId"]!.GetValue<int>());
        Assert.Equal($"/member/programs?tab=history&enrollmentId={seed.OriginalEnrollmentId}", notice["targetPath"]!.GetValue<string>());
    }

    [Fact]
    public async Task Legacy_postponement_notification_falls_back_through_the_trusted_replacement_link()
    {
        using var factory = new CoLearnXApiFactory();
        var seed = await SeedReplacementAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            db.Notifications.Add(new Notification
            {
                UserId = seed.MemberId,
                IntakeId = seed.ReplacementIntakeId,
                Code = "N-postpone-offer",
                Title = "Legacy postponed class",
                Body = "Legacy replacement invitation without EnrollmentId.",
            });
            await db.SaveChangesAsync();
        }

        using var member = await ApiClient.AsMemberAsync(factory);
        var inbox = await member.GetFromJsonAsync<JsonObject>("/api/notifications/my");
        var notice = inbox!["items"]!.AsArray().Single(item => item!["title"]!.GetValue<string>() == "Legacy postponed class");
        Assert.Equal(seed.OriginalEnrollmentId, notice!["enrollmentId"]!.GetValue<int>());
        Assert.Equal($"/member/programs?tab=history&enrollmentId={seed.OriginalEnrollmentId}", notice["targetPath"]!.GetValue<string>());
    }

    private static async Task<(int MemberId, int OriginalIntakeId, int ReplacementIntakeId, int OriginalEnrollmentId)> SeedReplacementAsync(
        CoLearnXApiFactory factory)
    {
        var originalIntakeId = await IntakeNotificationTests.SeedDueReservation(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var original = await db.CourseIntakes.Include(item => item.Sessions).SingleAsync(item => item.Id == originalIntakeId);
        var originalSession = original.Sessions.Single();
        var memberId = await db.Users.Where(item => item.Email == SeedData.MemberEmail).Select(item => item.Id).SingleAsync();
        var originalEnrollment = await db.Enrollments.SingleAsync(item => item.UserId == memberId && item.CourseSessionId == originalSession.Id);
        originalEnrollment.Status = EnrollmentStatus.Cancelled;
        originalEnrollment.PostponementEligible = true;
        original.CancelledAt = DateTime.UtcNow.AddHours(-1);
        original.CancellationReason = "MinimumEnrollmentNotMet";

        var now = DateTime.UtcNow;
        var replacement = new CourseIntake
        {
            CourseId = original.CourseId,
            TrainerId = original.TrainerId,
            ReplacementForIntakeId = original.Id,
            RegistrationOpensAt = now.AddDays(-1),
            RegistrationClosesAt = now.AddDays(1),
            StartsAt = now.AddDays(20),
            EndsAt = now.AddDays(21),
            Status = CourseIntakeStatus.Published,
            MinEnrollment = original.MinEnrollment,
        };
        db.CourseIntakes.Add(replacement);
        await db.SaveChangesAsync();
        db.CourseSessions.Add(new CourseSession
        {
            CourseIntakeId = replacement.Id,
            Label = "Replacement Session",
            StartsAt = replacement.StartsAt.AddHours(1),
            EndsAt = replacement.StartsAt.AddHours(3),
            PhysicalCapacity = 0,
        });
        await db.SaveChangesAsync();
        return (memberId, original.Id, replacement.Id, originalEnrollment.Id);
    }
}
