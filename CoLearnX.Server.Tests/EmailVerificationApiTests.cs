using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoLearnX.Server.Tests;

public class EmailVerificationApiTests : IDisposable
{
    private readonly CoLearnXApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Newly_registered_member_cannot_sign_in_before_email_verification()
    {
        using var client = ApiClient.Anonymous(_factory);
        var email = $"verify.{Guid.NewGuid():N}@colearnx.test";

        var registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", "Verify User", null),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, "Password123!", "Member"),
            ApiJson.Options);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Registration_requires_verification_without_issuing_a_session()
    {
        using var client = ApiClient.Anonymous(_factory);
        var email = $"pending.{Guid.NewGuid():N}@colearnx.test";

        var registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", "Pending User", null),
            ApiJson.Options);

        registration.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        Assert.Equal(string.Empty, body.RootElement.GetProperty("accessToken").GetString());
        Assert.True(body.RootElement.GetProperty("emailVerificationRequired").GetBoolean());
    }

    [Fact]
    public async Task Invalid_email_verification_token_is_rejected_with_stable_error()
    {
        using var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new { token = "invalid" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_VERIFICATION_TOKEN", (await ApiClient.ReadErrorAsync(response))?.Code);
    }

    [Fact]
    public async Task Registration_email_verifies_once_and_enables_member_login()
    {
        var mail = new CapturingMailSender();
        using var app = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailVerificationMailSender>();
            services.AddSingleton<IEmailVerificationMailSender>(mail);
        }));
        using var client = app.CreateClient();
        var email = $"confirm.{Guid.NewGuid():N}@colearnx.test";

        (await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, "Password123!", "Confirm User", null),
            ApiJson.Options)).EnsureSuccessStatusCode();

        var link = Assert.Single(mail.Messages).Link;
        Assert.Contains("/verify-email#token=", link);
        var token = link.Split("#token=")[1];
        var verified = await client.PostAsJsonAsync("/api/auth/verify-email", new { token });
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(email, "Password123!", "Member"),
                ApiJson.Options)).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/auth/verify-email", new { token })).StatusCode);
    }

    [Fact]
    public async Task Existing_sqlite_users_are_preserved_and_marked_verified_during_upgrade()
    {
        using var client = ApiClient.Anonymous(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var users = await db.Users.AsNoTracking().Select(x => new { x.Id, x.Email }).ToListAsync();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE EmailVerificationTokens");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users DROP COLUMN EmailVerifiedAt");

        await SeedData.InitializeAsync(db);
        await SeedData.InitializeAsync(db);

        Assert.Equal(users, await db.Users.AsNoTracking().Select(x => new { x.Id, x.Email }).ToListAsync());
        Assert.All(await db.Users.AsNoTracking().Select(x => x.EmailVerifiedAt).ToListAsync(), value => Assert.NotNull(value));
        Assert.Equal(0, await db.EmailVerificationTokens.CountAsync());
    }

    [Fact]
    public async Task Registration_does_not_send_a_verification_link_to_an_insecure_client_origin()
    {
        var mail = new CapturingMailSender();
        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["PasswordReset:ClientBaseUrl"] = "http://example.test" }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailVerificationMailSender>();
                services.AddSingleton<IEmailVerificationMailSender>(mail);
            });
        });
        using var client = app.CreateClient();

        (await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"unsafe.{Guid.NewGuid():N}@colearnx.test", "Password123!", "Unsafe Origin", null),
            ApiJson.Options)).EnsureSuccessStatusCode();

        Assert.Empty(mail.Messages);
    }

    private sealed class CapturingMailSender : IEmailVerificationMailSender
    {
        public ConcurrentQueue<(string Email, string Link)> Messages { get; } = new();

        public Task SendAsync(string email, string link, CancellationToken ct)
        {
            Messages.Enqueue((email, link));
            return Task.CompletedTask;
        }
    }
}
