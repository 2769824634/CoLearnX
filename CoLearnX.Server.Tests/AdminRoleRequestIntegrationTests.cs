using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CoLearnX.Server.Tests;

public class AdminRoleRequestIntegrationTests
{
    [Fact]
    public async Task Approve_GrantsRoleAndAuditsExactlyOnce_WhenReplayed()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var roleRequest = await FindPendingAsync(client, "Creator");

            var firstResponse = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Approve", "Portfolio meets the role criteria."));
            firstResponse.EnsureSuccessStatusCode();
            var firstResult = await firstResponse.Content
                .ReadFromJsonAsync<AdminRoleRequestReviewResultDto>();

            var replayResponse = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Approve", null));
            replayResponse.EnsureSuccessStatusCode();
            var replayResult = await replayResponse.Content
                .ReadFromJsonAsync<AdminRoleRequestReviewResultDto>();

            Assert.NotNull(firstResult);
            Assert.False(firstResult.AlreadyReviewed);
            Assert.Equal("Approved", firstResult.RoleRequest.Status);
            Assert.NotNull(replayResult);
            Assert.True(replayResult.AlreadyReviewed);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(1, await db.UserRoles.CountAsync(item =>
                item.UserId == roleRequest.UserId && item.Role == AppRole.Creator));
            Assert.Equal(1, await db.AuditLogs.CountAsync(log =>
                log.Action == "RoleRequestApproved"
                && log.EntityId == roleRequest.Id.ToString()));
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task Reject_RequiresReason_AndLeavesRequestPending()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var roleRequest = await FindPendingAsync(client, "Trainer");

            var response = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Reject", " "));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal("REJECTION_REASON_REQUIRED", error?.Code);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var stored = await db.RoleRequests.SingleAsync(item => item.Id == roleRequest.Id);
            Assert.Equal(RoleRequestStatus.Pending, stored.Status);
            Assert.Null(stored.ReviewedAt);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task Reject_IsIdempotent_ButOppositeDecisionConflicts()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var roleRequest = await FindPendingAsync(client, "Trainer");

            var firstResponse = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Reject", "More facilitation evidence is required."));
            firstResponse.EnsureSuccessStatusCode();

            var replayResponse = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Reject", null));
            replayResponse.EnsureSuccessStatusCode();
            var replayResult = await replayResponse.Content
                .ReadFromJsonAsync<AdminRoleRequestReviewResultDto>();
            Assert.True(replayResult?.AlreadyReviewed);

            var conflictResponse = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Approve", null));
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
            var error = await conflictResponse.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal("ROLE_REQUEST_ALREADY_REVIEWED", error?.Code);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var stored = await db.RoleRequests.SingleAsync(item => item.Id == roleRequest.Id);
            Assert.Equal(RoleRequestStatus.Rejected, stored.Status);
            Assert.Equal(1, await db.AuditLogs.CountAsync(log =>
                log.Action == "RoleRequestRejected"
                && log.EntityId == roleRequest.Id.ToString()));
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task Review_RejectsUnknownDecision()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var roleRequest = await FindPendingAsync(client, "Creator");

            var response = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Escalate", null));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal("INVALID_REVIEW_DECISION", error?.Code);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task Database_RejectsDuplicatePendingRoleRequest()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            _ = factory.Services;
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var existing = await db.RoleRequests.SingleAsync(item =>
                item.RequestedRole == AppRole.Creator && item.Status == RoleRequestStatus.Pending);
            db.RoleRequests.Add(new RoleRequest
            {
                UserId = existing.UserId,
                RequestedRole = existing.RequestedRole,
            });

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    private static async Task<HttpClient> CreateAdminClientAsync(RoleRequestApiFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
        });
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest(
            SeedData.AdminEmail,
            "Password123!"));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<AdminAuthResponse>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login.AccessToken);
        return client;
    }

    private static async Task<AdminRoleRequestDto> FindPendingAsync(HttpClient client, string requestedRole)
    {
        var roleRequests = await client.GetFromJsonAsync<List<AdminRoleRequestDto>>(
            "/api/admin/role-requests?status=Pending");
        return Assert.Single(roleRequests!, item => item.RequestedRole == requestedRole);
    }

    private sealed class RoleRequestApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"colearnx-d2-{Guid.NewGuid():N}.db");

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
