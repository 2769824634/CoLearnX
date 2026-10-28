using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CoLearnX.Server.Tests;

public sealed class CourseTrainerInvitationTests
{
    private const int Creator = 300;
    private const int OtherCreator = 301;
    private const int Alex = 302;
    private const int Blair = 303;
    private const int MemberOnly = 304;
    private const int InactiveTrainer = 305;
    private const int DualUser = 306;
    private const int Course = 300;
    private const int OtherCourse = 301;
    private const int DualCourse = 302;

    [Fact]
    public async Task Invited_trainer_accepts_and_joins_the_course_roster()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var creator = factory.UserClient(Creator, AppRole.Creator);
        using var alex = factory.UserClient(Alex, AppRole.Trainer);

        var invited = await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)));
        Assert.Equal("Invited", invited.Status);
        Assert.Equal("Alex Chen", invited.FullName);
        Assert.Equal("Inclusive design coach", invited.Headline);

        var invitations = await alex.GetFromJsonAsync<List<CourseInvitationDto>>("/api/trainer/course-invitations");
        var invitation = Assert.Single(invitations!);
        Assert.Equal(("ROS-300", "Roster Basics", "Roster Creator", "Invited"),
            (invitation.CourseCode, invitation.CourseTitle, invitation.CreatorName, invitation.Status));

        var accepted = await Read<CourseInvitationDto>(await alex.PostAsync($"/api/trainer/course-invitations/{invitation.Id}/accept", null));
        Assert.Equal("Accepted", accepted.Status);
        Assert.NotNull(accepted.RespondedAt);

        var roster = await creator.GetFromJsonAsync<List<CourseTrainerDto>>($"/api/creator/courses/{Course}/trainers");
        Assert.Equal("Accepted", Assert.Single(roster!).Status);

        var trainerNotice = Assert.Single((await Inbox(alex)).Items, item => item.Type == "N-course-invitation");
        Assert.Equal("/trainer/invitations", trainerNotice.TargetPath);
        Assert.Contains("ROS-300 Roster Basics", trainerNotice.Message);
        var creatorNotice = Assert.Single((await Inbox(creator)).Items, item => item.Type == "N-course-invitation-accepted");
        Assert.Equal("/creator/courses", creatorNotice.TargetPath);
        Assert.Equal("Alex Chen accepted your invitation to teach ROS-300 Roster Basics.", creatorNotice.Message);

        var emails = await factory.ReadAsync(db => db.Notifications.Where(n => n.UserId == Alex || n.UserId == Creator)
            .ToDictionaryAsync(n => n.Code, n => n.EmailPending));
        Assert.True(emails["N-course-invitation"]);
        Assert.False(emails["N-course-invitation-accepted"]);
    }

    [Fact]
    public async Task Declined_and_removed_trainers_can_be_invited_again()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var creator = factory.UserClient(Creator, AppRole.Creator);
        using var alex = factory.UserClient(Alex, AppRole.Trainer);

        await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)));
        var invitationId = (await alex.GetFromJsonAsync<List<CourseInvitationDto>>("/api/trainer/course-invitations"))!.Single().Id;
        Assert.Equal("Declined", (await Read<CourseInvitationDto>(await alex.PostAsync($"/api/trainer/course-invitations/{invitationId}/decline", null))).Status);
        Assert.Single((await Inbox(creator)).Items, item => item.Type == "N-course-invitation-declined");

        var again = await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)));
        Assert.Equal("Invited", again.Status);
        Assert.Null(again.RespondedAt);
        await Read<CourseInvitationDto>(await alex.PostAsync($"/api/trainer/course-invitations/{invitationId}/accept", null));

        var removed = await Read<CourseTrainerDto>(await creator.PostAsync($"/api/creator/courses/{Course}/trainers/{Alex}/remove", null));
        Assert.Equal("Removed", removed.Status);
        Assert.NotNull(removed.RemovedAt);
        var removedNotice = Assert.Single((await Inbox(alex)).Items, item => item.Type == "N-course-trainer-removed");
        Assert.Equal("/trainer/invitations", removedNotice.TargetPath);

        Assert.Equal("Invited", (await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)))).Status);
        Assert.Equal(3, (await Inbox(alex)).Items.Count(item => item.Type == "N-course-invitation"));
        Assert.Equal(1, await factory.ReadAsync(db => db.CourseTrainers.CountAsync(row => row.CourseId == Course)));
    }

    [Fact]
    public async Task Inviting_a_pending_or_accepted_trainer_again_is_a_conflict()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var creator = factory.UserClient(Creator, AppRole.Creator);
        using var alex = factory.UserClient(Alex, AppRole.Trainer);

        await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)));
        await Error(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)), 409, "TRAINER_ALREADY_INVITED");

        var invitationId = (await alex.GetFromJsonAsync<List<CourseInvitationDto>>("/api/trainer/course-invitations"))!.Single().Id;
        await Read<CourseInvitationDto>(await alex.PostAsync($"/api/trainer/course-invitations/{invitationId}/accept", null));
        await Error(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)), 409, "TRAINER_ALREADY_INVITED");
    }

    [Theory]
    [InlineData(MemberOnly)]
    [InlineData(InactiveTrainer)]
    [InlineData(999)]
    public async Task Only_active_trainers_can_be_invited(int userId)
    {
        using var factory = await RosterFactory.CreateAsync();
        using var creator = factory.UserClient(Creator, AppRole.Creator);

        await Error(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(userId)), 404, "TRAINER_NOT_FOUND");
    }

    [Fact]
    public async Task Creators_and_trainers_only_reach_their_own_courses_and_invitations()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var creator = factory.UserClient(Creator, AppRole.Creator);
        using var otherCreator = factory.UserClient(OtherCreator, AppRole.Creator);
        using var alex = factory.UserClient(Alex, AppRole.Trainer);
        using var blair = factory.UserClient(Blair, AppRole.Trainer);

        await Error(await creator.GetAsync($"/api/creator/courses/{OtherCourse}/trainers"), 404, "COURSE_NOT_FOUND");
        await Error(await creator.PostAsJsonAsync($"/api/creator/courses/{OtherCourse}/trainers", new InviteTrainerRequest(Alex)), 404, "COURSE_NOT_FOUND");
        await Error(await creator.PostAsync($"/api/creator/courses/{OtherCourse}/trainers/{Alex}/remove", null), 404, "COURSE_NOT_FOUND");

        await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)));
        await Error(await otherCreator.PostAsync($"/api/creator/courses/{Course}/trainers/{Alex}/remove", null), 404, "COURSE_NOT_FOUND");
        await Error(await creator.PostAsync($"/api/creator/courses/{Course}/trainers/{Blair}/remove", null), 404, "COURSE_TRAINER_NOT_FOUND");

        var invitationId = (await alex.GetFromJsonAsync<List<CourseInvitationDto>>("/api/trainer/course-invitations"))!.Single().Id;
        Assert.Empty((await blair.GetFromJsonAsync<List<CourseInvitationDto>>("/api/trainer/course-invitations"))!);
        await Error(await blair.PostAsync($"/api/trainer/course-invitations/{invitationId}/accept", null), 404, "INVITATION_NOT_FOUND");

        using var trainerAsCreator = await creator.GetAsync("/api/trainer/course-invitations");
        Assert.Equal(HttpStatusCode.Forbidden, trainerAsCreator.StatusCode);
    }

    [Fact]
    public async Task Repeating_a_finished_action_returns_the_current_state_without_new_notifications()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var creator = factory.UserClient(Creator, AppRole.Creator);
        using var alex = factory.UserClient(Alex, AppRole.Trainer);

        await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)));
        var invitationId = (await alex.GetFromJsonAsync<List<CourseInvitationDto>>("/api/trainer/course-invitations"))!.Single().Id;
        await Read<CourseInvitationDto>(await alex.PostAsync($"/api/trainer/course-invitations/{invitationId}/accept", null));
        Assert.Equal("Accepted", (await Read<CourseInvitationDto>(await alex.PostAsync($"/api/trainer/course-invitations/{invitationId}/accept", null))).Status);
        Assert.Single((await Inbox(creator)).Items, item => item.Type == "N-course-invitation-accepted");
        await Error(await alex.PostAsync($"/api/trainer/course-invitations/{invitationId}/decline", null), 409, "INVITATION_NOT_PENDING");

        await Read<CourseTrainerDto>(await creator.PostAsync($"/api/creator/courses/{Course}/trainers/{Alex}/remove", null));
        Assert.Equal("Removed", (await Read<CourseTrainerDto>(await creator.PostAsync($"/api/creator/courses/{Course}/trainers/{Alex}/remove", null))).Status);
        Assert.Single((await Inbox(alex)).Items, item => item.Type == "N-course-trainer-removed");
        await Error(await alex.PostAsync($"/api/trainer/course-invitations/{invitationId}/accept", null), 409, "INVITATION_NOT_PENDING");
    }

    [Fact]
    public async Task A_creator_who_is_also_a_trainer_joins_their_own_roster_directly()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var dual = factory.UserClient(DualUser, AppRole.Creator, AppRole.Creator, AppRole.Trainer);

        var self = await Read<CourseTrainerDto>(await dual.PostAsJsonAsync($"/api/creator/courses/{DualCourse}/trainers", new InviteTrainerRequest(DualUser)));

        Assert.Equal("Accepted", self.Status);
        Assert.NotNull(self.RespondedAt);
        Assert.Equal(0, await factory.ReadAsync(db => db.Notifications.CountAsync(n => n.UserId == DualUser)));
    }

    [Fact]
    public async Task Each_roster_change_leaves_one_audit_entry_by_the_person_who_made_it()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var creator = factory.UserClient(Creator, AppRole.Creator);
        using var alex = factory.UserClient(Alex, AppRole.Trainer);
        using var blair = factory.UserClient(Blair, AppRole.Trainer);

        await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)));
        await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Blair)));
        var alexInvitation = (await alex.GetFromJsonAsync<List<CourseInvitationDto>>("/api/trainer/course-invitations"))!.Single().Id;
        var blairInvitation = (await blair.GetFromJsonAsync<List<CourseInvitationDto>>("/api/trainer/course-invitations"))!.Single().Id;
        await Read<CourseInvitationDto>(await alex.PostAsync($"/api/trainer/course-invitations/{alexInvitation}/accept", null));
        await Read<CourseInvitationDto>(await alex.PostAsync($"/api/trainer/course-invitations/{alexInvitation}/accept", null));
        await Read<CourseInvitationDto>(await blair.PostAsync($"/api/trainer/course-invitations/{blairInvitation}/decline", null));
        await Read<CourseTrainerDto>(await creator.PostAsync($"/api/creator/courses/{Course}/trainers/{Alex}/remove", null));
        await Read<CourseTrainerDto>(await creator.PostAsync($"/api/creator/courses/{Course}/trainers/{Alex}/remove", null));

        var entries = await factory.ReadAsync(db => db.AuditLogs.Where(log => log.EntityType == "CourseTrainer")
            .OrderBy(log => log.Id)
            .Select(log => new { log.UserId, log.AdminAccountId, log.Action, log.EntityId, log.Result })
            .ToListAsync());

        Assert.Equal(new[]
        {
            (Creator, "CourseTrainerInvited", $"{Course}/{Alex}", "Invited"),
            (Creator, "CourseTrainerInvited", $"{Course}/{Blair}", "Invited"),
            (Alex, "CourseInvitationAccepted", $"{Course}/{Alex}", "Accepted"),
            (Blair, "CourseInvitationDeclined", $"{Course}/{Blair}", "Declined"),
            (Creator, "CourseTrainerRemoved", $"{Course}/{Alex}", "Removed"),
        }, entries.Select(entry => (entry.UserId!.Value, entry.Action, entry.EntityId!, entry.Result)).ToList());
        Assert.All(entries, entry => Assert.Null(entry.AdminAccountId));
    }

    [Fact]
    public async Task A_staff_trainers_invitation_response_is_audited_as_the_staff_account()
    {
        using var factory = await RosterFactory.CreateAsync();
        var staffId = await factory.ReadAsync(async db =>
        {
            var admin = new AdminAccount { Email = "roster-admin@example.com", PasswordHash = "x" };
            db.AdminAccounts.Add(admin);
            await db.SaveChangesAsync();
            var staff = new StaffAccount
            {
                Email = "alex-staff@example.com",
                PasswordHash = "x",
                FullName = "Alex Chen",
                DisplayName = "Alex",
                StaffNumber = 7,
                DocumentPath = "staff/7/document.pdf",
                UserId = Alex,
                CreatedByAdminAccountId = admin.Id,
                SessionStamp = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.StaffAccounts.Add(staff);
            await db.SaveChangesAsync();
            return staff.Id;
        });
        using var creator = factory.UserClient(Creator, AppRole.Creator);
        using var alex = factory.UserClient(Alex, AppRole.Trainer);
        await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)));
        var invitationId = (await alex.GetFromJsonAsync<List<CourseInvitationDto>>("/api/trainer/course-invitations"))!.Single().Id;
        await Read<CourseInvitationDto>(await alex.PostAsync($"/api/trainer/course-invitations/{invitationId}/accept", null));

        var entry = await factory.ReadAsync(db => db.AuditLogs.SingleAsync(log => log.Action == "CourseInvitationAccepted"));
        Assert.Equal(staffId, entry.StaffAccountId);
        Assert.Null(entry.UserId);
        Assert.Null(entry.AdminAccountId);
    }

    [Fact]
    public async Task A_creator_adding_themselves_is_audited_as_accepted()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var dual = factory.UserClient(DualUser, AppRole.Creator, AppRole.Creator, AppRole.Trainer);

        await Read<CourseTrainerDto>(await dual.PostAsJsonAsync($"/api/creator/courses/{DualCourse}/trainers", new InviteTrainerRequest(DualUser)));

        var entry = await factory.ReadAsync(db => db.AuditLogs.SingleAsync(log => log.EntityType == "CourseTrainer"));
        Assert.Equal((DualUser, "CourseTrainerInvited", $"{DualCourse}/{DualUser}", "Accepted"),
            (entry.UserId!.Value, entry.Action, entry.EntityId!, entry.Result));
    }

    [Fact]
    public async Task Importing_past_intake_trainers_writes_no_audit_entries()
    {
        using var factory = await RosterFactory.CreateAsync();

        Assert.True(await factory.ReadAsync(db => db.CourseTrainers.AnyAsync()));
        Assert.False(await factory.ReadAsync(db => db.AuditLogs.AnyAsync(log => log.EntityType == "CourseTrainer")));
    }

    [Fact]
    public async Task Trainer_search_lists_active_trainers_without_contact_details()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var creator = factory.UserClient(Creator, AppRole.Creator);

        using var response = await creator.GetAsync("/api/creator/trainers?search=ALEX");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var alex = Assert.Single(body.RootElement.EnumerateArray());
        Assert.Equal(Alex, alex.GetProperty("id").GetInt32());
        Assert.Equal("Inclusive design coach", alex.GetProperty("headline").GetString());
        Assert.Equal("Accessibility, Service design", alex.GetProperty("specialisations").GetString());
        Assert.False(alex.TryGetProperty("email", out _));

        var everyone = await creator.GetFromJsonAsync<List<TrainerCandidateDto>>("/api/creator/trainers");
        var names = everyone!.Select(item => item.FullName).ToList();
        Assert.Contains("Blair Ng", names);
        Assert.DoesNotContain("Casey Member", names);
        Assert.DoesNotContain("Drew Inactive", names);
        Assert.Equal(names.Order(StringComparer.OrdinalIgnoreCase), names);
        Assert.True(names.Count <= 20);
    }

    [Fact]
    public async Task Past_intake_trainers_start_on_the_roster_of_seeded_courses()
    {
        using var factory = await RosterFactory.CreateAsync();
        var (courseId, creatorId, trainerName) = await factory.ReadAsync(async db =>
        {
            var intake = await db.CourseIntakes.Include(i => i.Course).Include(i => i.Trainer).OrderBy(i => i.Id).FirstAsync();
            return (intake.CourseId, intake.Course.CreatorId, intake.Trainer.FullName);
        });
        using var creator = factory.UserClient(creatorId, AppRole.Creator);

        var roster = await creator.GetFromJsonAsync<List<CourseTrainerDto>>($"/api/creator/courses/{courseId}/trainers");

        var row = Assert.Single(roster!, item => item.FullName == trainerName);
        Assert.Equal("Accepted", row.Status);
    }

    [Fact]
    public async Task Invitation_is_pushed_to_the_trainer_immediately()
    {
        using var factory = await RosterFactory.CreateAsync();
        using var creator = factory.UserClient(Creator, AppRole.Creator);
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(factory.Token(Alex, AppRole.Trainer));
            })
            .Build();
        var received = new TaskCompletionSource<NotificationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<NotificationDto>("notification", notification => received.TrySetResult(notification));
        await connection.StartAsync();

        await Read<CourseTrainerDto>(await creator.PostAsJsonAsync($"/api/creator/courses/{Course}/trainers", new InviteTrainerRequest(Alex)));

        Assert.Same(received.Task, await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5))));
        var notification = await received.Task;
        Assert.Equal("N-course-invitation", notification.Type);
        Assert.Equal("/trainer/invitations", notification.TargetPath);
    }

    private static async Task<NotificationInboxDto> Inbox(HttpClient client)
        => (await client.GetFromJsonAsync<NotificationInboxDto>("/api/notifications/my"))!;

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<T>(ApiJson.Options))!;
        }
    }

    private static async Task Error(HttpResponseMessage response, int status, string code)
    {
        using (response)
        {
            Assert.Equal((HttpStatusCode)status, response.StatusCode);
            Assert.Equal(code, (await response.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options))!.Code);
        }
    }

    private sealed class RosterFactory : WebApplicationFactory<Program>
    {
        // A file database, because SignalR long polling queries it alongside API requests and one shared connection is not thread-safe.
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"colearnx-roster-{Guid.NewGuid():N}.db");
        private readonly string _uploadPath = Path.Combine(Path.GetTempPath(), $"colearnx-roster-uploads-{Guid.NewGuid():N}");

        public static async Task<RosterFactory> CreateAsync()
        {
            var factory = new RosterFactory();
            await factory.ReadAsync(async db =>
            {
                db.Users.AddRange(
                    new User { Id = Creator, Email = "roster-creator@example.com", FullName = "Roster Creator", Roles = [new UserRole { Role = AppRole.Creator }] },
                    new User { Id = OtherCreator, Email = "roster-other@example.com", FullName = "Other Creator", Roles = [new UserRole { Role = AppRole.Creator }] },
                    new User
                    {
                        Id = Alex, Email = "alex@example.com", FullName = "Alex Chen", Roles = [new UserRole { Role = AppRole.Trainer }],
                        TrainerProfile = new TrainerProfile { Headline = "Inclusive design coach", Specialisations = "Accessibility, Service design" },
                    },
                    new User { Id = Blair, Email = "blair@example.com", FullName = "Blair Ng", Roles = [new UserRole { Role = AppRole.Trainer }] },
                    new User { Id = MemberOnly, Email = "casey@example.com", FullName = "Casey Member", Roles = [new UserRole { Role = AppRole.Member }] },
                    new User { Id = InactiveTrainer, Email = "drew@example.com", FullName = "Drew Inactive", IsActive = false, Roles = [new UserRole { Role = AppRole.Trainer }] },
                    new User { Id = DualUser, Email = "erin@example.com", FullName = "Erin Dual", Roles = [new UserRole { Role = AppRole.Creator }, new UserRole { Role = AppRole.Trainer }] });
                db.Courses.AddRange(
                    new Course { Id = Course, Code = "ROS-300", Title = "Roster Basics", TrainerId = Creator, CreatorId = Creator, CreditCost = 10 },
                    new Course { Id = OtherCourse, Code = "ROS-301", Title = "Other Course", TrainerId = OtherCreator, CreatorId = OtherCreator, CreditCost = 10 },
                    new Course { Id = DualCourse, Code = "ROS-302", Title = "Dual Course", TrainerId = DualUser, CreatorId = DualUser, CreditCost = 10 });
                return await db.SaveChangesAsync();
            });
            return factory;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoLearnXDbContext>>();
                services.RemoveAll<CoLearnXDbContext>();
                services.AddDbContext<CoLearnXDbContext>(options => options.UseSqlite($"Data Source={_databasePath};Pooling=False"));
                services.RemoveAll<IFileStorage>();
                services.RemoveAll<IRoleRequestFileStorage>();
                services.AddSingleton<IFileStorage>(new LocalFileStorage(_uploadPath));
                services.AddSingleton<IRoleRequestFileStorage>(new RoleRequestFileStorage(
                    new LocalFileStorage(Path.Combine(_uploadPath, "role-requests"))));
            });
        }

        public string Token(int id, AppRole role, params AppRole[] roles)
        {
            using var scope = Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
                .CreateToken(new User { Id = id }, role, roles.Length == 0 ? [role] : roles).Token;
        }

        public HttpClient UserClient(int id, AppRole role, params AppRole[] roles)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(id, role, roles));
            return client;
        }

        public async Task<T> ReadAsync<T>(Func<CoLearnXDbContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>());
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;
            try
            {
                foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
                    if (File.Exists(path)) File.Delete(path);
                if (Directory.Exists(_uploadPath)) Directory.Delete(_uploadPath, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
