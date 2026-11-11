using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
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
            var roleRequest = await FindPendingAsync(factory, client, "Creator");

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
            var role = await db.UserRoles.SingleAsync(item => item.UserId == roleRequest.UserId);
            Assert.Equal(AppRole.Creator, role.Role);
            Assert.NotNull(role.CertifiedAt);
            Assert.Equal(1, await db.AuditLogs.CountAsync(log =>
                log.Action == "RoleRequestApproved"
                && log.EntityId == roleRequest.Id.ToString()));
            var notification = Assert.Single(await db.Notifications
                .Where(item => item.Code == "N-role-approved").ToListAsync());
            Assert.Equal(roleRequest.UserId, notification.UserId);
            Assert.Contains("Creator", notification.Body);
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
            var roleRequest = await FindPendingAsync(factory, client, "Trainer");

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
            var roleRequest = await FindPendingAsync(factory, client, "Trainer");

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
            var notification = Assert.Single(await db.Notifications
                .Where(item => item.Code == "N-role-rejected").ToListAsync());
            Assert.Equal(roleRequest.UserId, notification.UserId);
            Assert.Contains("More facilitation evidence is required.", notification.Body);
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
            var roleRequest = await FindPendingAsync(factory, client, "Creator");

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
    public async Task Approve_Succeeds_WhenSqlServerRetryOnFailureIsEnabled()
    {
        var database = $"colearnx-role-review-{Guid.NewGuid():N}";
        var connection =
            $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5";
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>()
            .UseSqlServer(connection, sql => sql.EnableRetryOnFailure())
            .Options;
        await using var db = new CoLearnXDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = new User
            {
                Email = $"applicant.{Guid.NewGuid():N}@colearnx.test",
                PasswordHash = "hash",
                FullName = "Online Applicant",
                DisplayName = "Applicant",
            };
            var admin = new AdminAccount { Email = $"admin.{Guid.NewGuid():N}@colearnx.test", PasswordHash = "hash" };
            db.Users.Add(user);
            db.AdminAccounts.Add(admin);
            await db.SaveChangesAsync();
            db.UserRoles.Add(new UserRole { UserId = user.Id, Role = AppRole.Trainer, IsVisible = true, CertifiedAt = null });
            var roleRequest = new RoleRequest
            {
                UserId = user.Id,
                RequestedRole = AppRole.Trainer,
                ApplicantStatement = "test online request",
            };
            db.RoleRequests.Add(roleRequest);
            await db.SaveChangesAsync();

            var service = new AdminRoleRequestService(db, new AuditLogService(db));
            var result = await service.ReviewAsync(
                admin.Id,
                roleRequest.Id,
                new AdminReviewRequest("Approve", null));

            Assert.False(result.AlreadyReviewed);
            Assert.Equal("Approved", result.RoleRequest.Status);
            Assert.Equal(1, await db.UserRoles.CountAsync(item =>
                item.UserId == user.Id && item.Role == AppRole.Trainer));
            var certified = await db.UserRoles.SingleAsync(item => item.UserId == user.Id);
            Assert.Equal(AppRole.Trainer, certified.Role);
            Assert.NotNull(certified.CertifiedAt);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Database_RejectsDuplicatePendingRoleRequest()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            var existing = await SeedPendingCertificationAsync(factory, AppRole.Creator);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
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

    [Fact]
    public async Task Approve_conflicts_when_the_account_does_not_hold_the_requested_role()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            int userId, requestId;
            using (var scope = factory.Services.CreateScope())
            {
                // A request left over from the old "Member applies for another role" flow.
                var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
                var user = new User { Email = $"legacy.{Guid.NewGuid():N}@colearnx.test", PasswordHash = "hash", FullName = "Legacy Member" };
                db.Users.Add(user);
                await db.SaveChangesAsync();
                db.UserRoles.Add(new UserRole { UserId = user.Id, Role = AppRole.Member });
                var request = new RoleRequest { UserId = user.Id, RequestedRole = AppRole.Trainer };
                db.RoleRequests.Add(request);
                await db.SaveChangesAsync();
                (userId, requestId) = (user.Id, request.Id);
            }

            var response = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{requestId}/review",
                new AdminReviewRequest("Approve", null));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("ROLE_NOT_HELD", (await response.Content.ReadFromJsonAsync<ApiError>())?.Code);
            using var check = factory.Services.CreateScope();
            var roles = await check.ServiceProvider.GetRequiredService<CoLearnXDbContext>().UserRoles
                .Where(item => item.UserId == userId).Select(item => item.Role).ToListAsync();
            Assert.Equal([AppRole.Member], roles);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    // A self-registered Trainer or Creator waiting for certification.
    private static async Task<RoleRequest> SeedPendingCertificationAsync(RoleRequestApiFactory factory, AppRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var user = new User { Email = $"applicant.{Guid.NewGuid():N}@colearnx.test", PasswordHash = "hash", FullName = $"{role} Applicant" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, Role = role, CertifiedAt = null });
        var request = new RoleRequest { UserId = user.Id, RequestedRole = role, ApplicantStatement = "Please certify me." };
        db.RoleRequests.Add(request);
        await db.SaveChangesAsync();
        return request;
    }

    private static async Task<AdminRoleRequestDto> FindPendingAsync(RoleRequestApiFactory factory, HttpClient client, string requestedRole)
    {
        await SeedPendingCertificationAsync(factory, Enum.Parse<AppRole>(requestedRole));
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
