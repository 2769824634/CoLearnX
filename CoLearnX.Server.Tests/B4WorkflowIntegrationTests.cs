using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CoLearnX.Server.Tests;

public sealed class B4WorkflowIntegrationTests
{
    private static readonly DateTime Start = new(2027, 4, 1, 9, 0, 0, DateTimeKind.Utc);
    private static CreateCourseIntakeRequest Draft => new(Start.AddDays(-30), Start.AddDays(-2), Start, Start.AddDays(2));

    [Fact]
    public async Task CreatorApplicationQueue_IsAvailableToCreator()
    {
        using var factory = new B4ApiFactory();
        using var creator = factory.UserClient(3, AppRole.Creator);

        using var response = await creator.GetAsync("/api/creator/intake-applications");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TrainerSubmit_CreatorConfirm_DeliveryAndConfirmedChange_RunEndToEnd()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(100, AppRole.Trainer);
        using var creator = factory.UserClient(103, AppRole.Creator);

        var intake = await CreateSubmittedAsync(trainer);
        var queue = await creator.GetFromJsonAsync<List<CreatorIntakeApplicationSummaryDto>>("/api/creator/intake-applications");
        var queued = Assert.Single(queue!, item => item.CourseIntakeId == intake.Id);
        Assert.Equal("Initial", queued.Kind);
        Assert.Equal("Pending", queued.Status);
        var application = await creator.GetFromJsonAsync<CreatorIntakeApplicationDetailDto>($"/api/creator/intake-applications/{intake.Id}");
        Assert.Null(application!.ProposedChange);

        intake = await Detail(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{intake.Id}/review",
            new ReviewIntakeApplicationRequest("Confirm", "Ready to deliver", queued.Version)));
        Assert.Equal("Published", intake.Status);
        Assert.Equal(103, intake.ConfirmedByCreatorId);

        var session = Assert.Single(intake.Sessions);
        intake = await Detail(await trainer.PatchAsJsonAsync($"/api/trainer/intakes/{intake.Id}/sessions/{session.Id}/delivery",
            new UpdateSessionDeliveryRequest("https://example.com/live-room", intake.Version)));
        Assert.Equal("https://example.com/live-room", Assert.Single(intake.Sessions).MeetingLink);

        var changedEnd = intake.EndsAt.AddDays(1);
        var proposal = new CreateCourseIntakeChangeRequest(intake.RegistrationOpensAt, intake.RegistrationClosesAt,
            intake.StartsAt, changedEnd,
            [new ProposedCourseSessionRequest(session.Id, "Advanced workshop", session.StartsAt, session.EndsAt,
                "https://example.com/live-room", null, 0, null)], intake.Version);
        var pending = await Detail(await trainer.PostAsJsonAsync($"/api/trainer/intakes/{intake.Id}/change-requests", proposal));
        Assert.Equal("PendingApproval", pending.Status);
        Assert.Equal(Draft.EndsAt, pending.EndsAt); // active confirmed data remains live during review
        Assert.Equal("Session 1", Assert.Single(pending.Sessions).Label);
        Assert.Equal("Advanced workshop", Assert.Single(pending.LatestChangeRequest!.Sessions).Label);

        application = await creator.GetFromJsonAsync<CreatorIntakeApplicationDetailDto>($"/api/creator/intake-applications/{intake.Id}");
        Assert.Equal("Change", application!.Application.Kind);
        Assert.Equal(changedEnd, application.ProposedChange!.EndsAt);
        intake = await Detail(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{intake.Id}/review",
            new ReviewIntakeApplicationRequest("Confirm", null, application.Application.Version)));
        Assert.Equal("Published", intake.Status);
        Assert.Equal(changedEnd, intake.EndsAt);
        Assert.Equal("Advanced workshop", Assert.Single(intake.Sessions).Label);

        var logs = await factory.ReadAsync(db => db.AuditLogs.Where(log => log.EntityType == nameof(CourseIntake)
            && log.EntityId == intake.Id.ToString()).ToListAsync());
        Assert.Contains(logs, log => log.Action == "CourseIntakeConfirmed" && log.UserId == 103);
        Assert.Contains(logs, log => log.Action == "CourseSessionDeliveryUpdated" && log.UserId == 100);
        Assert.Contains(logs, log => log.Action == "CourseIntakeChangeRequested" && log.UserId == 100);
    }

    [Fact]
    public async Task Reject_IsNotAdminReview_IsIdempotent_AndRequiresANote()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(100, AppRole.Trainer);
        using var creator = factory.UserClient(103, AppRole.Creator);
        using var otherCreator = factory.UserClient(104, AppRole.Creator);
        using var admin = factory.AdminClient();
        var intake = await CreateSubmittedAsync(trainer);

        await Error(await admin.GetAsync("/api/creator/intake-applications"), 401, "UNAUTHENTICATED");
        await Error(await otherCreator.GetAsync($"/api/creator/intake-applications/{intake.Id}"), 404, "INTAKE_APPLICATION_NOT_FOUND");
        await Error(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{intake.Id}/review",
            new ReviewIntakeApplicationRequest("Reject", null, intake.Version)), 400, "REJECTION_NOTE_REQUIRED");
        var rejected = await Detail(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{intake.Id}/review",
            new ReviewIntakeApplicationRequest("Reject", "Clarify the delivery dates", intake.Version)));
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Clarify the delivery dates", rejected.ConfirmationNote);

        var replay = await Detail(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{intake.Id}/review",
            new ReviewIntakeApplicationRequest("Reject", "Clarify the delivery dates", intake.Version)));
        Assert.Equal(rejected.Version, replay.Version);
        await Error(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{intake.Id}/review",
            new ReviewIntakeApplicationRequest("Confirm", null, intake.Version)), 409, "INTAKE_REVIEW_CONFLICT");
        var updated = await Detail(await trainer.PutAsJsonAsync($"/api/trainer/intakes/{intake.Id}",
            new UpdateCourseIntakeRequest(rejected.RegistrationOpensAt, rejected.RegistrationClosesAt,
                rejected.StartsAt, rejected.EndsAt.AddDays(1), rejected.Version)));
        Assert.Equal("Draft", updated.Status);
    }

    [Fact]
    public async Task CreatorCannotSelfReview_AndTrainerCannotUseAdminOrCreatorBoundaries()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var dual = factory.UserClient(105, AppRole.Trainer, AppRole.Trainer, AppRole.Creator);
        var draft = await Detail(await dual.PostAsJsonAsync("/api/trainer/courses/101/intakes", Draft), HttpStatusCode.Created);
        draft = await Detail(await dual.PostAsJsonAsync($"/api/trainer/intakes/{draft.Id}/sessions",
            new CreateCourseSessionRequest("Session 1", Start, Start.AddHours(2), "https://example.com", null, 0, null, draft.Version)));
        var pending = await Detail(await dual.PostAsJsonAsync($"/api/trainer/intakes/{draft.Id}/submit", new SubmitCourseIntakeRequest(draft.Version)));
        using var dualCreator = factory.UserClient(105, AppRole.Creator, AppRole.Trainer, AppRole.Creator);
        await Error(await dualCreator.PostAsJsonAsync($"/api/creator/intake-applications/{draft.Id}/review",
            new ReviewIntakeApplicationRequest("Confirm", null, pending.Version)), 403, "CREATOR_SELF_REVIEW_FORBIDDEN");
        await Error(await dual.GetAsync("/api/creator/intake-applications"), 403, "CREATOR_REQUIRED");
        await Error(await dualCreator.GetAsync("/api/trainer/intakes"), 403, "TRAINER_REQUIRED");
        using var member = factory.UserClient(102, AppRole.Member);
        await Error(await member.GetAsync("/api/creator/intake-applications"), 403, "CREATOR_REQUIRED");
    }

    [Fact]
    public async Task RejectedChange_RetainsLiveSchedule_AndCanBeRevisedAndResubmitted()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(100, AppRole.Trainer);
        using var creator = factory.UserClient(103, AppRole.Creator);
        var pending = await CreateSubmittedAsync(trainer);
        var published = await Detail(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{pending.Id}/review",
            new ReviewIntakeApplicationRequest("Confirm", null, pending.Version)));
        var session = Assert.Single(published.Sessions);
        var firstProposal = Change(published, session, "First proposed label");
        var changed = await Detail(await trainer.PostAsJsonAsync($"/api/trainer/intakes/{published.Id}/change-requests", firstProposal));
        var rejected = await Detail(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{published.Id}/review",
            new ReviewIntakeApplicationRequest("Reject", "Use the agreed workshop title", changed.Version)));
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Session 1", Assert.Single(rejected.Sessions).Label);
        Assert.Equal("First proposed label", Assert.Single(rejected.LatestChangeRequest!.Sessions).Label);
        await Error(await trainer.PutAsJsonAsync($"/api/trainer/intakes/{published.Id}",
            new UpdateCourseIntakeRequest(rejected.RegistrationOpensAt, rejected.RegistrationClosesAt,
                rejected.StartsAt, rejected.EndsAt, rejected.Version)), 409, "CHANGE_REQUEST_REQUIRED");

        var second = Change(rejected, Assert.Single(rejected.Sessions), "Agreed workshop title");
        var resubmitted = await Detail(await trainer.PostAsJsonAsync($"/api/trainer/intakes/{published.Id}/change-requests", second));
        Assert.Equal("PendingApproval", resubmitted.Status);
        var queue = await creator.GetFromJsonAsync<List<CreatorIntakeApplicationSummaryDto>>("/api/creator/intake-applications");
        Assert.Single(queue!, item => item.CourseIntakeId == published.Id);
        var confirmed = await Detail(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{published.Id}/review",
            new ReviewIntakeApplicationRequest("Confirm", null, resubmitted.Version)));
        Assert.Equal("Published", confirmed.Status);
        Assert.Equal("Agreed workshop title", Assert.Single(confirmed.Sessions).Label);
    }

    [Fact]
    public async Task DeliveryAndChangeEndpoints_EnforceWhitelistOwnershipVersionAndHistory()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(100, AppRole.Trainer);
        using var otherTrainer = factory.UserClient(101, AppRole.Trainer);
        using var creator = factory.UserClient(103, AppRole.Creator);
        var pending = await CreateSubmittedAsync(trainer);
        var session = Assert.Single(pending.Sessions);
        await Error(await trainer.PatchAsJsonAsync($"/api/trainer/intakes/{pending.Id}/sessions/{session.Id}/delivery",
            new UpdateSessionDeliveryRequest("https://example.com", pending.Version)), 409, "DELIVERY_NOT_EDITABLE");
        var published = await Detail(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{pending.Id}/review",
            new ReviewIntakeApplicationRequest("Confirm", null, pending.Version)));
        session = Assert.Single(published.Sessions);
        await Error(await otherTrainer.PatchAsJsonAsync($"/api/trainer/intakes/{published.Id}/sessions/{session.Id}/delivery",
            new UpdateSessionDeliveryRequest("https://example.com/forged", published.Version)), 404, "INTAKE_NOT_FOUND");
        await Error(await trainer.PatchAsJsonAsync($"/api/trainer/intakes/{published.Id}/sessions/{session.Id}/delivery",
            new { meetingLink = "https://example.com", version = published.Version, label = "overpost" }), 400, "INVALID_REQUEST");
        await Error(await trainer.PatchAsJsonAsync($"/api/trainer/intakes/{published.Id}/sessions/{session.Id}/delivery",
            new UpdateSessionDeliveryRequest("https://example.com", Guid.NewGuid())), 409, "INTAKE_VERSION_CONFLICT");

        await factory.ReadAsync(async db =>
        {
            db.Enrollments.Add(new Enrollment { UserId = 102, CourseId = 100, CourseSessionId = session.Id,
                CreditsSpent = 10, Status = EnrollmentStatus.Active });
            return await db.SaveChangesAsync();
        });
        await Error(await trainer.PostAsJsonAsync($"/api/trainer/intakes/{published.Id}/change-requests",
            Change(published, session, "Cannot rewrite history")), 409, "SESSION_HAS_HISTORY");
    }

    [Fact]
    public async Task InProgressIntake_AllowsRoutineMeetingLinkDelivery()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(100, AppRole.Trainer);
        using var creator = factory.UserClient(103, AppRole.Creator);
        var pending = await CreateSubmittedAsync(trainer);
        var published = await Detail(await creator.PostAsJsonAsync($"/api/creator/intake-applications/{pending.Id}/review",
            new ReviewIntakeApplicationRequest("Confirm", null, pending.Version)));
        await factory.ReadAsync(async db =>
        {
            (await db.CourseIntakes.FindAsync(published.Id))!.Status = CourseIntakeStatus.InProgress;
            return await db.SaveChangesAsync();
        });
        var session = Assert.Single(published.Sessions);
        var delivered = await Detail(await trainer.PatchAsJsonAsync($"/api/trainer/intakes/{published.Id}/sessions/{session.Id}/delivery",
            new UpdateSessionDeliveryRequest("https://example.com/in-progress", published.Version)));
        Assert.Equal("InProgress", delivered.Status);
        Assert.Equal("https://example.com/in-progress", Assert.Single(delivered.Sessions).MeetingLink);
    }

    private static CreateCourseIntakeChangeRequest Change(CourseIntakeDetailDto intake, CourseSessionDto session, string label)
        => new(intake.RegistrationOpensAt, intake.RegistrationClosesAt, intake.StartsAt, intake.EndsAt.AddDays(1),
            [new ProposedCourseSessionRequest(session.Id, label, session.StartsAt, session.EndsAt, session.MeetingLink,
                session.PhysicalAddress, session.PhysicalCapacity, session.PhysicalBookingDeadline)], intake.Version);

    private static async Task<CourseIntakeDetailDto> CreateSubmittedAsync(HttpClient trainer)
    {
        var intake = await Detail(await trainer.PostAsJsonAsync("/api/trainer/courses/100/intakes", Draft), HttpStatusCode.Created);
        intake = await Detail(await trainer.PostAsJsonAsync($"/api/trainer/intakes/{intake.Id}/sessions",
            new CreateCourseSessionRequest("Session 1", Start, Start.AddHours(2), "https://example.com/class", null, 0, null, intake.Version)));
        return await Detail(await trainer.PostAsJsonAsync($"/api/trainer/intakes/{intake.Id}/submit",
            new SubmitCourseIntakeRequest(intake.Version)));
    }

    private static async Task<CourseIntakeDetailDto> Detail(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<CourseIntakeDetailDto>())!;
        }
    }

    private static async Task Error(HttpResponseMessage response, int status, string code)
    {
        using (response)
        {
            Assert.Equal((HttpStatusCode)status, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal(code, error!.Code);
        }
    }

    private sealed class B4ApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _connection.Open();
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoLearnXDbContext>>();
                services.RemoveAll<CoLearnXDbContext>();
                services.AddDbContext<CoLearnXDbContext>(options => options.UseSqlite(_connection));
            });
        }

        public HttpClient UserClient(int id, AppRole role, params AppRole[] roles)
        {
            using var scope = Services.CreateScope();
            var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
                .CreateToken(new User { Id = id }, role, roles.Length == 0 ? [role] : roles).Token;
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public HttpClient AdminClient()
        {
            using var scope = Services.CreateScope();
            var token = scope.ServiceProvider.GetRequiredService<IAdminTokenService>()
                .CreateToken(new AdminAccount { Id = 1, Email = SeedData.AdminEmail }).Token;
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public async Task InitializeAsync()
        {
            await ReadAsync(async db =>
            {
                var users = new[]
                {
                    new User { Id = 100, Email = "b4-trainer@example.com", FullName = "B4 Trainer", Roles = [new UserRole { Role = AppRole.Trainer }] },
                    new User { Id = 101, Email = "b4-other-trainer@example.com", FullName = "Other Trainer", Roles = [new UserRole { Role = AppRole.Trainer }] },
                    new User { Id = 102, Email = "b4-member@example.com", FullName = "B4 Member", CreditBalance = 100, Roles = [new UserRole { Role = AppRole.Member }] },
                    new User { Id = 103, Email = "b4-creator@example.com", FullName = "B4 Creator", Roles = [new UserRole { Role = AppRole.Creator }] },
                    new User { Id = 104, Email = "b4-other-creator@example.com", FullName = "Other Creator", Roles = [new UserRole { Role = AppRole.Creator }] },
                    new User { Id = 105, Email = "b4-dual@example.com", FullName = "Dual User", Roles = [new UserRole { Role = AppRole.Trainer }, new UserRole { Role = AppRole.Creator }] },
                };
                db.Users.AddRange(users);
                db.Courses.AddRange(
                    new Course { Id = 100, Code = "B4-100", Title = "B4 Workflow", TrainerId = 100, CreatorId = 103, CreditCost = 10, Status = CourseStatus.Published },
                    new Course { Id = 101, Code = "B4-101", Title = "Self Review", TrainerId = 105, CreatorId = 105, CreditCost = 10, Status = CourseStatus.Published });
                return await db.SaveChangesAsync();
            });
        }

        public async Task<T> ReadAsync<T>(Func<CoLearnXDbContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>());
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }
    }
}
