using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CoLearnX.Server.Tests;

public class AdminAuditQueryIntegrationTests
{
    [Fact]
    public async Task CursorPages_RemainDistinct_WhenNewEventsArriveWithSameTimestamp()
    {
        using var factory = new AuditApiFactory();
        using var client = await factory.CreateAdminClientAsync();
        var expectedIds = await factory.AddEventsAsync(60);
        var first = await client.GetFromJsonAsync<List<AuditLogDto>>(
            "/api/admin/audit-logs?entityType=D4Test&limit=25");
        Assert.Equal(25, first!.Count);
        await factory.AddEventsAsync(1);
        var second = await client.GetFromJsonAsync<List<AuditLogDto>>(
            $"/api/admin/audit-logs?entityType=D4Test&limit=25&beforeId={first[^1].Id}");
        var third = await client.GetFromJsonAsync<List<AuditLogDto>>(
            $"/api/admin/audit-logs?entityType=D4Test&limit=25&beforeId={second![^1].Id}");
        var actualIds = first.Concat(second).Concat(third!).Select(log => log.Id).ToArray();
        Assert.Equal(expectedIds.OrderByDescending(id => id), actualIds);
        Assert.Equal(actualIds.Length, actualIds.Distinct().Count());
    }

    [Fact]
    public async Task Filters_CombineOnServer_PreserveActorReasonAndDoNotWriteLogs()
    {
        using var factory = new AuditApiFactory();
        using var client = await factory.CreateAdminClientAsync();
        await factory.AddEventsAsync(6);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var before = await db.AuditLogs.CountAsync();

        var adminLogs = await client.GetFromJsonAsync<List<AuditLogDto>>(
            "/api/admin/audit-logs?entityType=D4Test&actorType=Admin&result=Rejected");
        Assert.Equal(3, adminLogs!.Count);
        Assert.All(adminLogs, log => {
            Assert.NotNull(log.AdminAccountId);
            Assert.Null(log.UserId);
            Assert.Equal("Rejected", log.Result);
            Assert.Equal("Line one\nLine two <sample>", log.Reason);
        });
        var userLogs = await client.GetFromJsonAsync<List<AuditLogDto>>(
            "/api/admin/audit-logs?entityType=D4Test&actorType=User");
        Assert.Equal(3, userLogs!.Count);
        Assert.All(userLogs, log => { Assert.NotNull(log.UserId); Assert.Null(log.AdminAccountId); });
        var empty = await client.GetFromJsonAsync<List<AuditLogDto>>(
            "/api/admin/audit-logs?entityType=D4Test&actorType=User&result=Rejected");
        Assert.Empty(empty!);
        Assert.Equal(before, await db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=501")]
    [InlineData("beforeId=-1")]
    [InlineData("actorType=Everyone")]
    public async Task InvalidQuery_ReturnsBadRequest(string query)
    {
        using var factory = new AuditApiFactory();
        using var client = await factory.CreateAdminClientAsync();
        using var response = await client.GetAsync($"/api/admin/audit-logs?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilteredAudit_RejectsAnonymousAndOrdinaryUser(bool signInAsUser)
    {
        using var factory = new AuditApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (signInAsUser)
        {
            using var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest("huang.yousheng@colearnx.com", "Password123!", "Member"));
            loginResponse.EnsureSuccessStatusCode();
            var login = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        }
        using var response = await client.GetAsync("/api/admin/audit-logs?actorType=Admin&limit=25");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class AuditApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _connection.Open();
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services => {
                services.RemoveAll<DbContextOptions<CoLearnXDbContext>>();
                services.RemoveAll<CoLearnXDbContext>();
                services.AddDbContext<CoLearnXDbContext>(options => options.UseSqlite(_connection));
            });
        }

        public async Task<HttpClient> CreateAdminClientAsync()
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            using var response = await client.PostAsJsonAsync("/api/admin/auth/login",
                new AdminLoginRequest(SeedData.AdminEmail, SeedData.DemoPassword));
            response.EnsureSuccessStatusCode();
            var login = await response.Content.ReadFromJsonAsync<AdminAuthResponse>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
            return client;
        }

        public async Task<int[]> AddEventsAsync(int count)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var adminId = await db.AdminAccounts.Select(account => account.Id).FirstAsync();
            var userId = await db.Users.Select(user => user.Id).FirstAsync();
            var logs = Enumerable.Range(0, count).Select(index => new AuditLog {
                AdminAccountId = index % 2 == 0 ? adminId : null,
                UserId = index % 2 != 0 ? userId : null,
                Action = "D4TestEvent", EntityType = "D4Test", EntityId = index.ToString(),
                Result = index % 2 == 0 ? "Rejected" : "Succeeded",
                Reason = index % 2 == 0 ? "Line one\nLine two <sample>" : null,
                CreatedAt = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc),
            }).ToArray();
            db.AuditLogs.AddRange(logs);
            await db.SaveChangesAsync();
            return logs.Select(log => log.Id).ToArray();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }
    }
}
