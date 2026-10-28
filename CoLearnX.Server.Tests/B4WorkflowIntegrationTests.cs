using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
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
    private static CreateCourseIntakeRequest DraftFor(int trainerId) => new(Start.AddDays(-30), Start.AddDays(-10), Start, Start.AddDays(2), TrainerId: trainerId);

    [Fact]
    public async Task CreatorPublishesAssignedClass_AndTrainerUpdatesDelivery()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(100, AppRole.Trainer);
        using var creator = factory.UserClient(103, AppRole.Creator);

        var intake = await CreatePublishedAsync(creator);
        Assert.Equal("Published", intake.Status);
        Assert.Equal(103, intake.ConfirmedByCreatorId);
        Assert.Equal(100, intake.TrainerId);
        var inbox = await trainer.GetFromJsonAsync<NotificationInboxDto>("/api/notifications/my");
        var assigned = Assert.Single(inbox!.Items, item => item.Type == "N-intake-published");
        Assert.Equal($"/trainer/courses/intakes/{intake.Id}", assigned.TargetPath);

        var session = Assert.Single(intake.Sessions);
        intake = await Detail(await trainer.PatchAsJsonAsync($"/api/trainer/intakes/{intake.Id}/sessions/{session.Id}/delivery",
            new UpdateSessionDeliveryRequest("https://example.com/live-room", intake.Version)));
        Assert.Equal("https://example.com/live-room", Assert.Single(intake.Sessions).MeetingLink);

        var changedEnd = intake.EndsAt.AddDays(1);
        intake = await Detail(await creator.PutAsJsonAsync($"/api/creator/intakes/{intake.Id}/sessions/{session.Id}",
            new UpdateCourseSessionRequest("Advanced workshop", session.StartsAt, session.EndsAt,
                "https://example.com/live-room", null, 0, null, intake.Version)));
        intake = await Detail(await creator.PutAsJsonAsync($"/api/creator/intakes/{intake.Id}",
            new UpdateCourseIntakeRequest(intake.RegistrationOpensAt, intake.RegistrationClosesAt, intake.StartsAt, changedEnd, intake.Version)));
        Assert.Equal("Published", intake.Status);
        Assert.Equal(changedEnd, intake.EndsAt);
        Assert.Equal("Advanced workshop", Assert.Single(intake.Sessions).Label);

        var logs = await factory.ReadAsync(db => db.AuditLogs.Where(log => log.EntityType == nameof(CourseIntake)
            && log.EntityId == intake.Id.ToString()).ToListAsync());
        Assert.Contains(logs, log => log.Action == "CourseIntakePublished" && log.UserId == 103);
        Assert.Contains(logs, log => log.Action == "CourseSessionDeliveryUpdated" && log.UserId == 100);
        Assert.Contains(logs, log => log.Action == "CourseIntakeUpdated" && log.UserId == 103);
    }

    [Fact]
    public async Task CreatorCannotDeleteSessionAfterRealSessionMaterialUpload()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(100, AppRole.Trainer);
        using var creator = factory.UserClient(103, AppRole.Creator);

        var published = await CreatePublishedAsync(creator);
        var session = Assert.Single(published.Sessions);
        var title = $"Uploaded before removal {Guid.NewGuid():N}";
        var payload = "uploaded session material"u8.ToArray();
        using var uploadForm = PdfForm(title, payload);
        using var upload = await trainer.PostAsync($"/api/trainer/intakes/{published.Id}/sessions/{session.Id}/materials", uploadForm);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var material = await upload.Content.ReadFromJsonAsync<SessionMaterialDto>();
        Assert.NotNull(material);

        await Error(await creator.DeleteAsync($"/api/creator/intakes/{published.Id}/sessions/{session.Id}?version={published.Version}"),
            409, "SESSION_HAS_MATERIALS");

        var current = await trainer.GetFromJsonAsync<CourseIntakeDetailDto>($"/api/trainer/intakes/{published.Id}");
        Assert.Equal("Published", current!.Status);
        Assert.Contains(current.Sessions, item => item.Id == session.Id);
        Assert.True(await factory.ReadAsync(db => db.SessionMaterials.AnyAsync(item => item.Id == material!.Id)));
        using var preserved = await trainer.GetAsync(
            $"/api/trainer/intakes/{published.Id}/sessions/{session.Id}/materials/{material.Id}/file");
        Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
        Assert.Equal(payload, await preserved.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task IntakeOwnership_StaysWithTheCourseCreator()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var creator = factory.UserClient(103, AppRole.Creator);
        using var otherCreator = factory.UserClient(104, AppRole.Creator);
        using var admin = factory.AdminClient();
        using var member = factory.UserClient(102, AppRole.Member);
        var intake = await CreatePublishedAsync(creator);

        await Error(await admin.GetAsync("/api/creator/intakes"), 401, "UNAUTHENTICATED");
        await Error(await otherCreator.GetAsync($"/api/creator/intakes/{intake.Id}"), 404, "INTAKE_NOT_FOUND");
        await Error(await member.GetAsync("/api/creator/intakes"), 403, "CREATOR_REQUIRED");
        var replay = await Detail(await creator.PostAsJsonAsync($"/api/creator/intakes/{intake.Id}/publish",
            new SubmitCourseIntakeRequest(intake.Version)));
        Assert.Equal(intake.Version, replay.Version);
        Assert.Equal(1, await factory.ReadAsync(db => db.AuditLogs.CountAsync(log => log.Action == "CourseIntakePublished" && log.EntityId == intake.Id.ToString())));
    }

    [Fact]
    public async Task CourseCreatorWhoAlsoTeaches_PublishesWithoutASecondRoleToken()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var dualTrainer = factory.UserClient(105, AppRole.Trainer, AppRole.Trainer, AppRole.Creator);
        using var dualCreator = factory.UserClient(105, AppRole.Creator, AppRole.Trainer, AppRole.Creator);
        var published = await CreatePublishedAsync(dualCreator, 101, 105);
        Assert.Equal("Published", published.Status);
        Assert.Equal(105, published.ConfirmedByCreatorId);
        Assert.Equal(published.Id, Assert.Single((await dualTrainer.GetFromJsonAsync<List<CourseIntakeSummaryDto>>("/api/trainer/intakes"))!).Id);
        await Error(await dualTrainer.PostAsJsonAsync("/api/creator/courses/101/intakes", DraftFor(105)), 403, "CREATOR_REQUIRED");
        await Error(await dualCreator.GetAsync("/api/trainer/intakes"), 403, "TRAINER_REQUIRED");
    }

    [Fact]
    public async Task PublishedSchedule_CanBeEditedUntilALearnerCommits()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var creator = factory.UserClient(103, AppRole.Creator);
        var published = await CreatePublishedAsync(creator);
        var session = Assert.Single(published.Sessions);
        var edited = await Detail(await creator.PutAsJsonAsync($"/api/creator/intakes/{published.Id}/sessions/{session.Id}",
            new UpdateCourseSessionRequest("Agreed workshop title", session.StartsAt, session.EndsAt,
                session.MeetingLink, null, 0, null, published.Version)));
        Assert.Equal("Published", edited.Status);
        Assert.Equal("Agreed workshop title", Assert.Single(edited.Sessions).Label);

        await factory.ReadAsync(async db =>
        {
            db.Enrollments.Add(new Enrollment { UserId = 102, CourseId = 100, CourseSessionId = session.Id,
                CreditsSpent = 10, Status = EnrollmentStatus.Active });
            return await db.SaveChangesAsync();
        });
        await Error(await creator.PutAsJsonAsync($"/api/creator/intakes/{published.Id}",
            new UpdateCourseIntakeRequest(edited.RegistrationOpensAt, edited.RegistrationClosesAt,
                edited.StartsAt, edited.EndsAt, edited.Version)), 409, "INTAKE_HAS_COMMITTED_ENROLLMENTS");
    }

    [Fact]
    public async Task DeliveryAndStructuralEdits_EnforceWhitelistOwnershipVersionAndCommitments()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(100, AppRole.Trainer);
        using var otherTrainer = factory.UserClient(101, AppRole.Trainer);
        using var creator = factory.UserClient(103, AppRole.Creator);
        var draft = await Detail(await creator.PostAsJsonAsync("/api/creator/courses/100/intakes", DraftFor(100)), HttpStatusCode.Created);
        draft = await Detail(await creator.PostAsJsonAsync($"/api/creator/intakes/{draft.Id}/sessions",
            new CreateCourseSessionRequest("Session 1", Start, Start.AddHours(2), "https://example.com/class", null, 0, null, draft.Version)));
        var session = Assert.Single(draft.Sessions);
        await Error(await trainer.PatchAsJsonAsync($"/api/trainer/intakes/{draft.Id}/sessions/{session.Id}/delivery",
            new UpdateSessionDeliveryRequest("https://example.com", draft.Version)), 409, "DELIVERY_NOT_EDITABLE");
        var published = await Detail(await creator.PostAsJsonAsync($"/api/creator/intakes/{draft.Id}/publish",
            new SubmitCourseIntakeRequest(draft.Version)));
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
        await Error(await creator.PutAsJsonAsync($"/api/creator/intakes/{published.Id}/sessions/{session.Id}",
            new UpdateCourseSessionRequest("Cannot rewrite history", session.StartsAt, session.EndsAt,
                session.MeetingLink, null, 0, null, published.Version)), 409, "INTAKE_HAS_COMMITTED_ENROLLMENTS");
        var delivered = await Detail(await trainer.PatchAsJsonAsync($"/api/trainer/intakes/{published.Id}/sessions/{session.Id}/delivery",
            new UpdateSessionDeliveryRequest("https://example.com/still-open", published.Version)));
        Assert.Equal("https://example.com/still-open", Assert.Single(delivered.Sessions).MeetingLink);
    }

    [Fact]
    public async Task InProgressIntake_AllowsRoutineMeetingLinkDelivery()
    {
        using var factory = new B4ApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(100, AppRole.Trainer);
        using var creator = factory.UserClient(103, AppRole.Creator);
        var published = await CreatePublishedAsync(creator);
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

    private static async Task<CourseIntakeDetailDto> CreatePublishedAsync(HttpClient creator, int courseId = 100, int trainerId = 100)
    {
        var intake = await Detail(await creator.PostAsJsonAsync($"/api/creator/courses/{courseId}/intakes", DraftFor(trainerId)), HttpStatusCode.Created);
        intake = await Detail(await creator.PostAsJsonAsync($"/api/creator/intakes/{intake.Id}/sessions",
            new CreateCourseSessionRequest("Session 1", Start, Start.AddHours(2), "https://example.com/class", null, 0, null, intake.Version)));
        return await Detail(await creator.PostAsJsonAsync($"/api/creator/intakes/{intake.Id}/publish",
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

    private static MultipartFormDataContent PdfForm(string title, byte[] payload)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        var file = new ByteArrayContent(payload);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "session-material.pdf");
        return form;
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
        private readonly string _uploadPath = Path.Combine(Path.GetTempPath(), $"colearnx-b4-uploads-{Guid.NewGuid():N}");

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
                services.RemoveAll<IFileStorage>();
                services.RemoveAll<IRoleRequestFileStorage>();
                services.AddSingleton<IFileStorage>(new LocalFileStorage(_uploadPath));
                services.AddSingleton<IRoleRequestFileStorage>(new RoleRequestFileStorage(
                    new LocalFileStorage(Path.Combine(_uploadPath, "role-requests"))));
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
                db.CourseTrainers.AddRange(
                    new CourseTrainer { CourseId = 100, TrainerId = 100, Status = CourseTrainerStatus.Accepted, InvitedByUserId = 103, InvitedAt = DateTime.UtcNow, RespondedAt = DateTime.UtcNow },
                    new CourseTrainer { CourseId = 101, TrainerId = 105, Status = CourseTrainerStatus.Accepted, InvitedByUserId = 105, InvitedAt = DateTime.UtcNow, RespondedAt = DateTime.UtcNow });
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
            if (disposing)
            {
                _connection.Dispose();
                TryDeleteDirectory(_uploadPath);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
