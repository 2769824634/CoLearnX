using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Collections.Concurrent;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoLearnX.Server.Tests;

public class PasswordResetApiTests : IDisposable
{
    private readonly CoLearnXApiFactory _factory = new();
    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Existing_sqlite_schema_is_upgraded_idempotently_without_losing_users()
    {
        using var client = ApiClient.Anonymous(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var users = await db.Users.AsNoTracking().Select(u => new { u.Id, u.Email, u.PasswordHash }).ToListAsync();
        // This factory owns a disposable temporary database, not any developer/user database.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE PasswordResetTokens");
        await SeedData.InitializeAsync(db);
        await SeedData.InitializeAsync(db);
        Assert.Equal(0, await db.PasswordResetTokens.CountAsync());
        Assert.Equal(users, await db.Users.AsNoTracking().Select(u => new { u.Id, u.Email, u.PasswordHash }).ToListAsync());
    }

    [Fact]
    public async Task Unknown_account_receives_generic_success()
    {
        var client = ApiClient.Anonymous(_factory);
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "missing@colearnx.test" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("If the account is eligible", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reset_changes_password_invalidates_session_and_cannot_be_replayed()
    {
        var mail = new CapturingMailSender();
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordResetMailSender>();
            services.AddSingleton<IPasswordResetMailSender>(mail);
        }));
        var client = app.CreateClient();
        var email = $"reset.{Guid.NewGuid():N}@colearnx.test";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Password123!", "Reset User", null));
        registration.EnsureSuccessStatusCode();
        var auth = (await registration.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var request = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        request.EnsureSuccessStatusCode();
        var generic = await request.Content.ReadAsStringAsync();
        var token = mail.Messages.Single().Link.Split("#token=")[1];
        Assert.DoesNotContain(token, generic);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var row = await db.PasswordResetTokens.SingleAsync(x => x.UserId == auth.User.Id);
            Assert.NotEqual(token, row.TokenHash);
            Assert.Equal(64, row.TokenHash.Length);
        }
        var repeated = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(generic, await repeated.Content.ReadAsStringAsync());
        Assert.Single(mail.Messages);
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown@colearnx.test" });
        Assert.Equal(generic, await unknown.Content.ReadAsStringAsync());
        var admin = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = SeedData.AdminEmail });
        Assert.Equal(generic, await admin.Content.ReadAsStringAsync());
        Assert.Single(mail.Messages);

        var weak = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "weakpassword" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal("WEAK_PASSWORD", (await ApiClient.ReadErrorAsync(weak))?.Code);
        var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "Different789!" });
        reset.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        var replay = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "ThirdChoice456!" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("INVALID_RESET_TOKEN", (await ApiClient.ReadErrorAsync(replay))?.Code);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!", "Member"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Different789!", "Member"))).StatusCode);
    }

    [Theory]
    [InlineData(SeedData.MemberEmail)]
    [InlineData(SeedData.TrainerEmail)]
    [InlineData(SeedData.CreatorEmail)]
    public async Task Ordinary_roles_support_reset_with_exactly_one_concurrent_consumer(string email)
    {
        var mail = new CapturingMailSender();
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordResetMailSender>();
            services.AddSingleton<IPasswordResetMailSender>(mail);
        }));
        var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = mail.Messages.Single().Link.Split("#token=")[1];
        // Each request creates its own service scope and database connection.
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "Changed789!" })));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, responses.Count(x => x.StatusCode == HttpStatusCode.BadRequest));
    }

    [Fact]
    public async Task Expired_forged_and_inactive_user_credentials_are_rejected()
    {
        var mail = new CapturingMailSender();
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordResetMailSender>();
            services.AddSingleton<IPasswordResetMailSender>(mail);
        }));
        var client = app.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = SeedData.MemberEmail });
        var token = mail.Messages.Single().Link.Split("#token=")[1];
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            await db.PasswordResetTokens.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }
        foreach (var invalid in new[] { token, new string('F', 64), "forged" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", new { token = invalid, newPassword = "Changed789!" })).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            await db.PasswordResetTokens.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(10)));
            await db.Users.Where(x => x.Email == SeedData.MemberEmail).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = "Changed789!" })).StatusCode);
    }

    [Fact]
    public async Task Delivery_failure_and_ip_rate_limit_preserve_generic_response()
    {
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordResetMailSender>();
            services.AddSingleton<IPasswordResetMailSender>(new FailingMailSender());
        }));
        var client = app.CreateClient();
        string? expected = null;
        for (var attempt = 0; attempt < 12; attempt++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = attempt == 0 ? SeedData.MemberEmail : $"missing{attempt}@colearnx.test" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            expected ??= body;
            Assert.Equal(expected, body);
        }
    }

    private sealed class CapturingMailSender : IPasswordResetMailSender
    {
        public ConcurrentQueue<(string Email, string Link)> Messages { get; } = new();
        public Task SendAsync(string email, string resetLink, CancellationToken ct)
        {
            Messages.Enqueue((email, resetLink));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingMailSender : IPasswordResetMailSender
    {
        public Task SendAsync(string email, string resetLink, CancellationToken ct) =>
            throw new InvalidOperationException("Production mail not configured");
    }
}
