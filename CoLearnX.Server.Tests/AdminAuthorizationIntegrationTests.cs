using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CoLearnX.Server.Tests;

public class AdminAuthorizationIntegrationTests
{
    private static readonly JwtOptions TestJwtOptions = new()
    {
        Issuer = "CoLearnX",
        Audience = "CoLearnX.Client",
        SigningKey = "CoLearnX-Dev-Signing-Key-Change-In-Production-Min-32-Chars",
        ExpiryMinutes = 15,
    };

    [Fact]
    public async Task AdminPolicy_RejectsPreviouslyIssuedTokenAfterAccountIsDisabled()
    {
        var factory = new AdminApiFactory();
        try
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });
            var token = await LoginAsAdminAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var beforeDisable = await client.GetAsync("/api/admin/audit-logs");
            Assert.Equal(HttpStatusCode.OK, beforeDisable.StatusCode);

            await factory.SetDemoAdminActiveAsync(false);

            var afterDisable = await client.GetAsync("/api/admin/audit-logs");
            Assert.Equal(HttpStatusCode.Forbidden, afterDisable.StatusCode);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task AdminPolicy_RejectsOrdinaryUserToken()
    {
        var factory = new AdminApiFactory();
        try
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
                "huang.yousheng@colearnx.com",
                "Password123!",
                "Member"));
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<AuthResponse>();
            Assert.NotNull(login);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

            var adminResponse = await client.GetAsync("/api/admin/audit-logs");

            Assert.Equal(HttpStatusCode.Unauthorized, adminResponse.StatusCode);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task AdminPolicy_RejectsTokenForUnknownAdminAccount()
    {
        var factory = new AdminApiFactory();
        try
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });
            _ = factory.Services;
            var tokenService = new AdminTokenService(Options.Create(TestJwtOptions));
            var (token, _) = tokenService.CreateToken(new AdminAccount
            {
                Id = int.MaxValue,
                Email = "missing-admin@colearnx.test",
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/admin/audit-logs");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task AdminScheme_RejectsExpiredTokenWithoutClockSkew()
    {
        var factory = new AdminApiFactory();
        try
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });
            _ = factory.Services;
            var expiredOptions = new JwtOptions
            {
                Issuer = TestJwtOptions.Issuer,
                Audience = TestJwtOptions.Audience,
                SigningKey = TestJwtOptions.SigningKey,
                ExpiryMinutes = -1,
            };
            var tokenService = new AdminTokenService(Options.Create(expiredOptions));
            var (token, _) = tokenService.CreateToken(new AdminAccount
            {
                Id = 1,
                Email = SeedData.AdminEmail,
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync("/api/admin/audit-logs");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    private static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest(
            SeedData.AdminEmail,
            SeedData.DemoPassword));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<AdminAuthResponse>();
        Assert.NotNull(login);
        return login.AccessToken;
    }

    private sealed class AdminApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"colearnx-d1b-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoLearnXDbContext>>();
                services.RemoveAll<CoLearnXDbContext>();
                services.AddDbContext<CoLearnXDbContext>(options =>
                    options.UseSqlite($"Data Source={_databasePath};Pooling=False"));
            });
        }

        public async Task SetDemoAdminActiveAsync(bool isActive)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var admin = await db.AdminAccounts.SingleAsync(
                account => account.Email == SeedData.AdminEmail);
            admin.IsActive = isActive;
            admin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        public void DeleteDatabase()
        {
            foreach (var path in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
