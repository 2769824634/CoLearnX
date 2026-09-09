using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CoLearnX.Server.Tests;

public class TrainerIntakesIntegrationTests
{
    private static readonly DateTime Start = new(2027, 2, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static CreateCourseIntakeRequest Draft => new(Start.AddDays(-20), Start.AddDays(-1), Start, Start.AddDays(3));
    private static CreateCourseSessionRequest Session(Guid version) => new("Session 1", Start, Start.AddHours(1), "https://example.com/class", null, 0, null, version);
    private static string Intake(int id) => $"/api/trainer/intakes/{id}";
    private const string CreatePath = "/api/trainer/courses/100/intakes";

    [Fact]
    public async Task RealLogin_CreateReadEditSessionDeleteAndSubmit_WorkAcrossRequests()
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.Anonymous();
        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("b2-100@example.com", "Password123!", "Trainer"));
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        using var creation = await client.PostAsJsonAsync(CreatePath, Draft);
        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        var d = (await creation.Content.ReadFromJsonAsync<CourseIntakeDetailDto>())!;
        Assert.EndsWith(Intake(d.Id), creation.Headers.Location!.ToString());
        Assert.Equal(100, d.TrainerId);
        Assert.Equal("Draft", d.Status);
        Assert.Empty(d.Sessions);
        Assert.Equal(d.Id, Assert.Single((await client.GetFromJsonAsync<List<CourseIntakeSummaryDto>>("/api/trainer/intakes"))!).Id);
        var reread = await client.GetFromJsonAsync<CourseIntakeDetailDto>(creation.Headers.Location);
        Assert.Equal(d.Version, reread!.Version);
        Assert.Equal(DateTimeKind.Utc, reread.StartsAt.Kind);
        d = await Detail(await client.PutAsJsonAsync(Intake(d.Id), new UpdateCourseIntakeRequest(
            Draft.RegistrationOpensAt, Draft.RegistrationClosesAt, Start, Start.AddDays(4), d.Version)));
        d = await Detail(await client.PostAsJsonAsync(Intake(d.Id) + "/sessions", Session(d.Version)));
        var sid = Assert.Single(d.Sessions).Id;
        d = await Detail(await client.PutAsJsonAsync(Intake(d.Id) + $"/sessions/{sid}", new UpdateCourseSessionRequest(
            "Updated session", Start, Start.AddHours(2), null, "Room 1", 20, Start.AddDays(-1), d.Version)));
        Assert.Equal("Updated session", Assert.Single(d.Sessions).Label);
        d = await Detail(await client.DeleteAsync(Intake(d.Id) + $"/sessions/{sid}?version={d.Version}"));
        Assert.Empty(d.Sessions);
        d = await Detail(await client.PostAsJsonAsync(Intake(d.Id) + "/sessions", Session(d.Version)));
        d = await Detail(await client.PostAsJsonAsync(Intake(d.Id) + "/submit", new SubmitCourseIntakeRequest(d.Version)));
        Assert.Equal("PendingApproval", d.Status);
        Assert.NotNull(d.SubmittedAt);
        var logs = await f.ReadAsync(db => db.AuditLogs.Where(a => a.EntityType == "CourseIntake" && a.EntityId == d.Id.ToString()).ToListAsync());
        Assert.Equal(7, logs.Count);
        Assert.All(logs, log => { Assert.Equal(100, log.UserId); Assert.Null(log.AdminAccountId); });
    }

    [Theory]
    [InlineData("anonymous", 401)]
    [InlineData("admin", 401)]
    [InlineData("member", 403)]
    [InlineData("creator", 403)]
    [InlineData("expired", 401)]
    [InlineData("legacy", 401)]
    [InlineData("forged-role", 403)]
    [InlineData("missing-id", 401)]
    public async Task AllEightEndpoints_EnforceIdentityBoundary(string identity, int status)
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var owner = f.UserClient(100);
        var d = await Detail(await owner.PostAsJsonAsync(CreatePath, Draft), HttpStatusCode.Created);
        using var client = f.IdentityClient(identity);
        foreach (var request in Requests(d))
        {
            using (request)
                await Error(await client.SendAsync(request), status, status == 401 ? "UNAUTHENTICATED" : "TRAINER_REQUIRED");
        }
        Assert.Equal(1, await f.ReadAsync(db => db.CourseIntakes.CountAsync(i => i.TrainerId == 100)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IssuedToken_IsDeniedAfterAccountOrTrainerRoleRevocation(bool removeRole)
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.UserClient(100);
        await f.ReadAsync(async db => {
            if (removeRole) db.UserRoles.Remove(await db.UserRoles.SingleAsync(r => r.UserId == 100 && r.Role == AppRole.Trainer));
            else (await db.Users.FindAsync(100))!.IsActive = false;
            return await db.SaveChangesAsync();
        });
        await Error(await client.GetAsync("/api/trainer/intakes"), 403, "TRAINER_REQUIRED");
        await Error(await client.PostAsJsonAsync(CreatePath, Draft), 403, "TRAINER_REQUIRED");
    }

    [Fact]
    public async Task OtherTrainer_SeesNoIntakeAndCannotReadOrMutateIt()
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var owner = f.UserClient(100);
        var d = await Detail(await owner.PostAsJsonAsync(CreatePath, Draft), HttpStatusCode.Created);
        using var other = f.UserClient(101);
        Assert.Empty((await other.GetFromJsonAsync<List<CourseIntakeSummaryDto>>("/api/trainer/intakes"))!);
        foreach (var request in Requests(d).Skip(2))
        {
            using (request) await Error(await other.SendAsync(request), 404, "INTAKE_NOT_FOUND");
        }
        Assert.Equal(d.Version, (await owner.GetFromJsonAsync<CourseIntakeDetailDto>(Intake(d.Id)))!.Version);
    }

    [Theory]
    [InlineData("trainerId")]
    [InlineData("TrainerId")]
    [InlineData("status")]
    [InlineData("confirmedByCreatorId")]
    [InlineData("confirmedAt")]
    [InlineData("confirmationNote")]
    public async Task Create_RejectsOverpostedOwnershipStateAndConfirmation(string forbidden)
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.UserClient(100);
        var json = JsonSerializer.SerializeToNode(Draft, Json)!.AsObject();
        json[forbidden] = "forged";
        await Error(await client.PostAsJsonAsync(CreatePath, json), 400, "INVALID_REQUEST", true);
        Assert.Equal(0, await f.ReadAsync(db => db.CourseIntakes.CountAsync(i => i.TrainerId == 100)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("[]")]
    public async Task MalformedOrMissingCreatePayload_HasUniformValidationEnvelope(string json)
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.UserClient(100);
        await Error(await client.PostAsync(CreatePath, new StringContent(json, Encoding.UTF8, "application/json")), 400, "INVALID_REQUEST", true);
    }

    [Fact]
    public async Task OtherWriteBodies_AlsoRejectOverpostedFieldsWithoutMutating()
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.UserClient(100);
        var d = await Detail(await client.PostAsJsonAsync(CreatePath, Draft), HttpStatusCode.Created);
        var update = JsonSerializer.SerializeToNode(new UpdateCourseIntakeRequest(Draft.RegistrationOpensAt,
            Draft.RegistrationClosesAt, Start, Start.AddDays(3), d.Version), Json)!.AsObject();
        update["trainerId"] = 101;
        await Error(await client.PutAsJsonAsync(Intake(d.Id), update), 400, "INVALID_REQUEST", true);
        var session = JsonSerializer.SerializeToNode(Session(d.Version), Json)!.AsObject();
        session["courseIntakeId"] = 999;
        await Error(await client.PostAsJsonAsync(Intake(d.Id) + "/sessions", session), 400, "INVALID_REQUEST", true);
        session.Remove("courseIntakeId");
        session["status"] = "Published";
        await Error(await client.PutAsJsonAsync(Intake(d.Id) + "/sessions/1", session), 400, "INVALID_REQUEST", true);
        await Error(await client.PostAsJsonAsync(Intake(d.Id) + "/submit", new { d.Version, confirmedByCreatorId = 103 }), 400, "INVALID_REQUEST", true);
        Assert.Equal(d.Version, (await client.GetFromJsonAsync<CourseIntakeDetailDto>(Intake(d.Id)))!.Version);
        Assert.Equal(1, await f.ReadAsync(db => db.AuditLogs.CountAsync(a => a.EntityType == "CourseIntake" && a.EntityId == d.Id.ToString())));
    }

    [Fact]
    public async Task DomainErrors_ExposeFieldErrorsAnd409WithoutSideEffects()
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.UserClient(100);
        await Error(await client.PostAsJsonAsync(CreatePath, Draft with { EndsAt = Start }), 400, "INVALID_INTAKE_DATES", true);
        await Error(await client.PostAsJsonAsync("/api/trainer/courses/999999/intakes", Draft), 409, "COURSE_NOT_PUBLISHED");
        var d = await Detail(await client.PostAsJsonAsync(CreatePath, Draft), HttpStatusCode.Created);
        await Error(await client.PostAsJsonAsync(Intake(d.Id) + "/submit", new SubmitCourseIntakeRequest(d.Version)), 400, "SESSION_REQUIRED", true);
        await Error(await client.PostAsJsonAsync(Intake(d.Id) + "/sessions", Session(d.Version) with { StartsAt = Start.AddDays(-1) }), 400, "SESSION_OUTSIDE_INTAKE", true);
        await Error(await client.PostAsJsonAsync(Intake(d.Id) + "/sessions", Session(d.Version) with { MeetingLink = "javascript:alert(1)" }), 400, "INVALID_MEETING_LINK", true);
        d = await Detail(await client.PostAsJsonAsync(Intake(d.Id) + "/sessions", Session(d.Version)));
        await Error(await client.DeleteAsync(Intake(d.Id) + "/sessions/99999?version=" + d.Version), 404, "SESSION_NOT_FOUND");
        Assert.Equal(1, await f.ReadAsync(db => db.CourseSessions.CountAsync(s => s.CourseIntakeId == d.Id)));
    }

    [Fact]
    public async Task MissingOrMalformedVersions_Are400_StaleVersionIs409()
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.UserClient(100);
        var d = await Detail(await client.PostAsJsonAsync(CreatePath, Draft), HttpStatusCode.Created);
        var missingVersion = JsonSerializer.SerializeToNode(Session(d.Version), Json)!.AsObject();
        missingVersion.Remove("version");
        await Error(await client.PostAsJsonAsync(Intake(d.Id) + "/sessions", missingVersion), 400, "INVALID_REQUEST", true);
        await Error(await client.PostAsJsonAsync(Intake(d.Id) + "/submit", new { }), 400, "INVALID_REQUEST", true);
        await Error(await client.PostAsJsonAsync(Intake(d.Id) + "/submit", new { version = "not-a-guid" }), 400, "INVALID_REQUEST", true);
        await Error(await client.DeleteAsync(Intake(d.Id) + "/sessions/1"), 400, "INVALID_REQUEST", true);
        await Error(await client.DeleteAsync(Intake(d.Id) + "/sessions/1?version=invalid"), 400, "INVALID_REQUEST", true);
        var scheduled = await Detail(await client.PostAsJsonAsync(Intake(d.Id) + "/sessions", Session(d.Version)));
        await Error(await client.PostAsJsonAsync(Intake(d.Id) + "/submit", new SubmitCourseIntakeRequest(d.Version)), 409, "INTAKE_VERSION_CONFLICT");
        Assert.Equal(scheduled.Version, (await client.GetFromJsonAsync<CourseIntakeDetailDto>(Intake(d.Id)))!.Version);
    }

    [Fact]
    public async Task PendingApproval_LocksEveryStructuralRoute_AndReplayDoesNotDuplicateAudit()
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.UserClient(100);
        var d = await Detail(await client.PostAsJsonAsync(CreatePath, Draft), HttpStatusCode.Created);
        d = await Detail(await client.PostAsJsonAsync(Intake(d.Id) + "/sessions", Session(d.Version)));
        d = await Detail(await client.PostAsJsonAsync(Intake(d.Id) + "/submit", new SubmitCourseIntakeRequest(d.Version)));
        foreach (var request in Requests(d).Skip(3).Take(4))
        {
            using (request) await Error(await client.SendAsync(request), 409, "INTAKE_NOT_EDITABLE");
        }
        var replay = await Detail(await client.PostAsJsonAsync(Intake(d.Id) + "/submit", new SubmitCourseIntakeRequest(d.Version)));
        Assert.Equal(d.Version, replay.Version);
        Assert.Equal(1, await f.ReadAsync(db => db.AuditLogs.CountAsync(a => a.Action == "CourseIntakeSubmitted" && a.EntityId == d.Id.ToString())));
    }

    [Fact]
    public async Task SessionFromDifferentOwnedIntake_CannotBeChangedBySwappingItsId()
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.UserClient(100);
        var first = await Detail(await client.PostAsJsonAsync(CreatePath, Draft), HttpStatusCode.Created);
        first = await Detail(await client.PostAsJsonAsync(Intake(first.Id) + "/sessions", Session(first.Version)));
        var second = await Detail(await client.PostAsJsonAsync(CreatePath, Draft), HttpStatusCode.Created);
        var sid = Assert.Single(first.Sessions).Id;
        await Error(await client.DeleteAsync(Intake(second.Id) + $"/sessions/{sid}?version={second.Version}"), 404, "SESSION_NOT_FOUND");
        await Error(await client.PutAsJsonAsync(Intake(second.Id) + $"/sessions/{sid}", new UpdateCourseSessionRequest("Forged", Start,
            Start.AddHours(1), "https://example.com", null, 0, null, second.Version)), 404, "SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task AuditStorageFailure_ReturnsSafe500_AndCreationRollsBack()
    {
        using var f = new TrainerApiFactory();
        await f.InitializeAsync();
        using var client = f.UserClient(100);
        f.FailAudit = true;
        using var response = await client.PostAsJsonAsync(CreatePath, Draft);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret storage detail", body);
        await Error(response, 500, "INTAKE_SAVE_FAILED");
        Assert.Equal(0, await f.ReadAsync(db => db.CourseIntakes.CountAsync(i => i.TrainerId == 100)));
    }

    private static IEnumerable<HttpRequestMessage> Requests(CourseIntakeDetailDto d)
    {
        // Order: list, create, get, update, add session, update session, delete session, submit.
        yield return new(HttpMethod.Get, "/api/trainer/intakes");
        yield return new(HttpMethod.Post, CreatePath) { Content = JsonContent.Create(Draft) };
        yield return new(HttpMethod.Get, Intake(d.Id));
        yield return new(HttpMethod.Put, Intake(d.Id)) { Content = JsonContent.Create(new UpdateCourseIntakeRequest(
            Draft.RegistrationOpensAt, Draft.RegistrationClosesAt, Start, Start.AddDays(3), d.Version)) };
        yield return new(HttpMethod.Post, Intake(d.Id) + "/sessions") { Content = JsonContent.Create(Session(d.Version)) };
        var sid = d.Sessions.FirstOrDefault()?.Id ?? 1;
        yield return new(HttpMethod.Put, Intake(d.Id) + $"/sessions/{sid}") { Content = JsonContent.Create(new UpdateCourseSessionRequest(
            "Edit", Start, Start.AddHours(1), "https://example.com", null, 0, null, d.Version)) };
        yield return new(HttpMethod.Delete, Intake(d.Id) + $"/sessions/{sid}?version={d.Version}");
        yield return new(HttpMethod.Post, Intake(d.Id) + "/submit") { Content = JsonContent.Create(new SubmitCourseIntakeRequest(d.Version)) };
    }

    private static async Task<CourseIntakeDetailDto> Detail(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<CourseIntakeDetailDto>())!;
        }
    }
    private static async Task Error(HttpResponseMessage response, int status, string code, bool hasFields = false)
    {
        using (response)
        {
            Assert.Equal((HttpStatusCode)status, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal(code, error!.Code);
            Assert.False(string.IsNullOrWhiteSpace(error.Message));
            Assert.NotNull(error.FieldErrors);
            if (hasFields) Assert.NotEmpty(error.FieldErrors);
        }
    }

    private sealed class TrainerApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        public bool FailAudit { get; set; }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _connection.Open();
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services => {
                services.RemoveAll<DbContextOptions<CoLearnXDbContext>>();
                services.RemoveAll<CoLearnXDbContext>();
                services.AddDbContext<CoLearnXDbContext>(options => options.UseSqlite(_connection).AddInterceptors(new AuditFailureInterceptor(this)));
            });
        }
        public async Task InitializeAsync()
        {
            await ReadAsync(async db => {
                var hash = BCrypt.Net.BCrypt.HashPassword("Password123!");
                db.Users.AddRange(Enumerable.Range(100, 4).Select(id => new User { Id = id, Email = $"b2-{id}@example.com",
                    FullName = $"B2 User {id}", PasswordHash = hash,
                    Roles = [new UserRole { Role = id < 102 ? AppRole.Trainer : id == 102 ? AppRole.Member : AppRole.Creator }] }));
                db.Courses.Add(new Course { Id = 100, Code = "B2", Title = "B2 API fixture", TrainerId = 100, CreatorId = 103, Status = CourseStatus.Published });
                return await db.SaveChangesAsync();
            });
        }
        public HttpClient Anonymous() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        public HttpClient UserClient(int id)
        {
            using var scope = Services.CreateScope();
            var role = id < 102 ? AppRole.Trainer : id == 102 ? AppRole.Member : AppRole.Creator;
            var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().CreateToken(new User { Id = id }, role, [role]).Token;
            return WithToken(token);
        }
        public HttpClient IdentityClient(string identity)
        {
            if (identity == "anonymous") return Anonymous();
            if (identity == "member") return UserClient(102);
            if (identity == "creator") return UserClient(103);
            if (identity == "admin")
            {
                using var scope = Services.CreateScope();
                // Intentional numeric ID collision with an eligible Trainer User.
                var token = scope.ServiceProvider.GetRequiredService<IAdminTokenService>().CreateToken(new AdminAccount { Id = 100, Email = "admin@example.com" }).Token;
                return WithToken(token);
            }
            var options = Services.GetRequiredService<IOptions<JwtOptions>>().Value;
            var claims = new List<Claim> { new(ClaimTypes.Role, "Trainer"), new("active_role", "Trainer") };
            if (identity != "missing-id") claims.Add(new(ClaimTypes.NameIdentifier, identity == "forged-role" ? "102" : "100"));
            if (identity != "legacy") claims.Add(new(AuthTokenSubjects.ClaimType, AuthTokenSubjects.User));
            var jwt = new JwtSecurityToken(options.Issuer, options.Audience, claims,
                expires: DateTime.UtcNow.AddMinutes(identity == "expired" ? -5 : 5),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256));
            return WithToken(new JwtSecurityTokenHandler().WriteToken(jwt));
        }
        private HttpClient WithToken(string token)
        {
            var client = Anonymous();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }
        public async Task<T> ReadAsync<T>(Func<CoLearnXDbContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>());
        }
        protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing) _connection.Dispose(); }
        private sealed class AuditFailureInterceptor(TrainerApiFactory factory) : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
                InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (factory.FailAudit && eventData.Context!.ChangeTracker.Entries<AuditLog>().Any(e =>
                    e.State == EntityState.Added && e.Entity.EntityType == "CourseIntake"))
                    throw new DbUpdateException("secret storage detail");
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }
    }
}
