using CoLearnX.Server.Data;
using CoLearnX.Server.Services;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoLearnX.Server.Tests;

public class NotificationEmailTests
{
    [Fact]
    public async Task ConfirmationReminderAndConfirmedCancellationDeliverTheSameBusinessEventsOnce()
    {
        using var factory = new CoLearnXApiFactory();
        var id = await IntakeNotificationTests.SeedDueReservation(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var intake = await db.CourseIntakes.Include(i => i.Sessions).SingleAsync(i => i.Id == id);
        await db.CourseIntakes.Where(i => i.Id != id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, CourseIntakeStatus.Completed));
        intake.MinEnrollment = 2;
        var second = new User { Email = "second-mail-learner@example.test", FullName = "Test learner", HeldCredits = 35 };
        var session = intake.Sessions.Single();
        session.SeatsTaken++;
        db.Enrollments.Add(new Enrollment { User = second, CourseId = intake.CourseId, CourseSessionId = session.Id, Status = EnrollmentStatus.Reserved, CreditsSpent = 35 });
        await db.SaveChangesAsync();
        var settlement = scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>();
        await settlement.SettleIfDueAsync(id);
        var sender = new RecordingMail();
        var dispatcher = new NotificationEmailDispatcher(db, sender, NullLogger<NotificationEmailDispatcher>.Instance);
        await dispatcher.DispatchPendingAsync();
        Assert.Equal(4, sender.Messages.Count);
        Assert.Equal(2, sender.Messages.Count(m => m.Body.Contains("Your place")));
        await db.CourseIntakes.Where(i => i.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(i => i.RegistrationOpensAt, DateTime.UtcNow.AddDays(-10))
            .SetProperty(i => i.RegistrationClosesAt, DateTime.UtcNow.AddDays(-5))
            .SetProperty(i => i.StartsAt, DateTime.UtcNow.AddDays(6)));
        await db.CourseSessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(s => s
            .SetProperty(i => i.StartsAt, DateTime.UtcNow.AddDays(6))
            .SetProperty(i => i.EndsAt, DateTime.UtcNow.AddDays(6).AddHours(2)));
        await settlement.ProcessDueAsync();
        await settlement.ProcessDueAsync();
        await dispatcher.DispatchPendingAsync();
        Assert.Equal(6, sender.Messages.Count);
        Assert.Equal(2, sender.Messages.Count(m => m.Body.Contains("Location:")));
        await settlement.CancelIntakeAsync(intake.TrainerId, id);
        await settlement.CancelIntakeAsync(intake.TrainerId, id);
        await dispatcher.DispatchPendingAsync();
        await dispatcher.DispatchPendingAsync();
        Assert.Equal(10, sender.Messages.Count);
        Assert.Equal(2, sender.Messages.Count(m => m.Body.Contains("credits were returned")));
    }

    [Fact]
    public async Task ExistingMailTransportCapturesBusinessMailWithSafeHtmlAndConfiguredLink()
    {
        var root = Path.Combine(Path.GetTempPath(), $"colearnx-business-mail-{Guid.NewGuid():N}");
        try
        {
            IBusinessNotificationMailSender sender = new PasswordResetMailSender(
                Options.Create(new PasswordResetOptions { ClientBaseUrl = "https://colearnx.example", FromAddress = "noreply@colearnx.test" }),
                new MailEnvironment { ContentRootPath = root });
            Assert.True(sender.IsAvailable);
            await sender.SendAsync("learner@example.test", "Class confirmed", "Class <Design> is ready", "/member/programs", CancellationToken.None);
            var file = Assert.Single(Directory.GetFiles(Path.Combine(root, "App_Data", "mail"), "*.eml"));
            var content = await File.ReadAllTextAsync(file);
            Assert.Contains("learner@example.test", content);
            Assert.Contains("https://colearnx.example/member/programs", MailTestBodies.Decode(content));
            var composed = AccountMail.BusinessNotification("Class confirmed", "Class <Design> is ready", "https://colearnx.example/member/programs");
            Assert.Contains("&lt;Design&gt;", composed.HtmlBody);
            Assert.Contains("Class <Design>", composed.PlainTextBody);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SettlementQueuesMailAndFailureDoesNotUndoReleaseThenRetrySendsOnce()
    {
        using var factory = new CoLearnXApiFactory();
        var id = await IntakeNotificationTests.SeedDueReservation(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(id);
        var sender = new RecordingMail { Fail = true };
        var dispatcher = new NotificationEmailDispatcher(db, sender, NullLogger<NotificationEmailDispatcher>.Instance);
        await dispatcher.DispatchPendingAsync();
        db.ChangeTracker.Clear();
        var user = await db.Users.SingleAsync(u => u.Email == SeedData.MemberEmail);
        Assert.Equal(120, user.CreditBalance);
        Assert.Equal(0, user.HeldCredits);
        Assert.True(await db.Notifications.AnyAsync(n => n.IntakeId == id && n.EmailPending));
        sender.Fail = false;
        await db.Notifications.Where(n => n.IntakeId == id).ExecuteUpdateAsync(s => s.SetProperty(n => n.EmailAttemptedAt, (DateTime?)null));
        await dispatcher.DispatchPendingAsync();
        await dispatcher.DispatchPendingAsync();
        Assert.Equal(3, sender.Messages.Count);
        Assert.Contains(sender.Messages, m => m.Path == $"/trainer/courses/intakes/{id}");
        Assert.Contains(sender.Messages, m => m.Path == $"/creator/courses/intake-applications/{id}");
        Assert.Contains(sender.Messages, m => m.Email == SeedData.MemberEmail && m.Body.Contains("7 days"));
        Assert.False(await db.Notifications.AnyAsync(n => n.IntakeId == id && n.EmailPending));
        Assert.Equal(3, await db.Notifications.CountAsync(n => n.IntakeId == id && n.EmailSentAt != null));
    }

    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 2)]
    public async Task UnavailableChannelAndMemberOptOutAreRespected(bool available, bool optedIn, int expected)
    {
        using var factory = new CoLearnXApiFactory();
        var id = await IntakeNotificationTests.SeedDueReservation(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        await db.UserPreferences.Where(p => p.User.Email == SeedData.MemberEmail)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.EmailNotifications, optedIn));
        await scope.ServiceProvider.GetRequiredService<IIntakeSettlementService>().SettleIfDueAsync(id);
        var sender = new RecordingMail { IsAvailable = available };
        await new NotificationEmailDispatcher(db, sender, NullLogger<NotificationEmailDispatcher>.Instance).DispatchPendingAsync();
        Assert.Equal(expected, sender.Messages.Count);
        Assert.Equal(3, await db.Notifications.CountAsync(n => n.IntakeId == id));
    }

    private sealed class RecordingMail : IBusinessNotificationMailSender
    {
        public bool IsAvailable { get; set; } = true;
        public bool Fail { get; set; }
        public List<(string Email, string Body, string Path)> Messages { get; } = [];
        public Task SendAsync(string email, string title, string body, string targetPath, CancellationToken ct)
        {
            if (Fail) throw new InvalidOperationException("Simulated delivery failure");
            Messages.Add((email, body, targetPath));
            return Task.CompletedTask;
        }
    }

    private sealed class MailEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
