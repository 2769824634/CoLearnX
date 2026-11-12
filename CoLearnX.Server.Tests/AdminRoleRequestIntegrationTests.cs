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
    [Theory]
    [InlineData(AppRole.Member, AppRole.Trainer)]
    [InlineData(AppRole.Trainer, AppRole.Creator)]
    [InlineData(AppRole.Creator, AppRole.Trainer)]
    public async Task Legacy_cross_role_request_cannot_be_approved_but_can_be_rejected(AppRole heldRole, AppRole requestedRole)
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            int requestId;
            int userId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
                var applicant = new User
                {
                    Email = $"legacy.{Guid.NewGuid():N}@colearnx.test",
                    PasswordHash = "hash",
                    FullName = "Legacy Applicant",
                    DisplayName = "Applicant",
                };
                db.Users.Add(applicant);
                await db.SaveChangesAsync();
                db.UserRoles.Add(new UserRole { UserId = applicant.Id, Role = heldRole });
                var application = new RoleRequest { UserId = applicant.Id, RequestedRole = requestedRole };
                db.RoleRequests.Add(application);
                await db.SaveChangesAsync();
                requestId = application.Id;
                userId = applicant.Id;
            }

            var approval = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{requestId}/review", new AdminReviewRequest("Approve", null));
            Assert.Equal(HttpStatusCode.Conflict, approval.StatusCode);
            Assert.Equal("ROLE_NOT_REQUESTABLE", (await approval.Content.ReadFromJsonAsync<ApiError>())?.Code);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
                Assert.Equal(RoleRequestStatus.Pending, (await db.RoleRequests.SingleAsync(item => item.Id == requestId)).Status);
                Assert.Equal(1, await db.UserRoles.CountAsync(item => item.UserId == userId));
                Assert.False(await db.AuditLogs.AnyAsync(item => item.EntityId == requestId.ToString() && item.Action == "RoleRequestApproved"));
            }

            var rejection = await client.PostAsJsonAsync(
                $"/api/admin/role-requests/{requestId}/review", new AdminReviewRequest("Reject", "Accounts cannot request another role."));
            Assert.Equal(HttpStatusCode.OK, rejection.StatusCode);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task Approve_CertifiesRoleAndAuditsExactlyOnce_WhenReplayed()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            await AddCertificationAsync(factory, AppRole.Creator);
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
            Assert.NotNull((await db.UserRoles.SingleAsync(item => item.UserId == roleRequest.UserId)).CertifiedAt);
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
            await AddCertificationAsync(factory, AppRole.Trainer);
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
            await AddCertificationAsync(factory, AppRole.Trainer);
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
    public async Task Approve_certifies_the_specialties_the_admin_corrected()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var (chosen, corrected, names) = await AddCertificationWithSpecialtiesAsync(factory, AppRole.Trainer);
            var roleRequest = await FindPendingAsync(client, "Trainer");
            Assert.Equal(chosen, roleRequest.SpecialtyIds);

            var options = await client.GetFromJsonAsync<List<SpecialtyOptionDto>>("/api/admin/role-requests/specialties");
            Assert.Contains(options!, option => option.Id == corrected[0]);

            var response = await client.PostAsJsonAsync($"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Approve", "Evidence supports two of the three areas.", corrected));
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AdminRoleRequestReviewResultDto>();
            Assert.Equal(corrected, result!.RoleRequest.SpecialtyIds);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var certified = await db.UserSpecialties.Where(item => item.UserId == roleRequest.UserId)
                .Select(item => item.CourseSubjectId).OrderBy(id => id).ToListAsync();
            Assert.Equal(corrected.OrderBy(id => id), certified);
            var notification = Assert.Single(await db.Notifications
                .Where(item => item.UserId == roleRequest.UserId && item.Code == "N-role-approved").ToListAsync());
            foreach (var id in corrected) Assert.Contains(names[id], notification.Body);
            var audit = await db.AuditLogs.SingleAsync(log => log.Action == "RoleRequestApproved" && log.EntityId == roleRequest.Id.ToString());
            Assert.Contains("Specialties changed", audit.Reason);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task Approve_without_a_correction_keeps_the_applicants_specialties()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var (chosen, _, _) = await AddCertificationWithSpecialtiesAsync(factory, AppRole.Creator);
            var roleRequest = await FindPendingAsync(client, "Creator");

            (await client.PostAsJsonAsync($"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Approve", null))).EnsureSuccessStatusCode();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var certified = await db.UserSpecialties.Where(item => item.UserId == roleRequest.UserId)
                .Select(item => item.CourseSubjectId).OrderBy(id => id).ToListAsync();
            Assert.Equal(chosen.OrderBy(id => id), certified);
            var audit = await db.AuditLogs.SingleAsync(log => log.Action == "RoleRequestApproved" && log.EntityId == roleRequest.Id.ToString());
            Assert.DoesNotContain("Specialties changed", audit.Reason ?? string.Empty);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Theory]
    [InlineData("none", "SPECIALTIES_REQUIRED")]
    [InlineData("six", "TOO_MANY_SPECIALTIES")]
    [InlineData("unknown", "SPECIALTY_NOT_FOUND")]
    public async Task Approve_rejects_an_invalid_correction_and_leaves_the_request_pending(string scenario, string code)
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var (chosen, _, names) = await AddCertificationWithSpecialtiesAsync(factory, AppRole.Trainer);
            var roleRequest = await FindPendingAsync(client, "Trainer");
            var all = names.Keys.OrderBy(id => id).ToList();
            int[] correction = scenario switch
            {
                "none" => [],
                "six" => all.Take(6).ToArray(),
                _ => [all[0], 999999],
            };

            var response = await client.PostAsJsonAsync($"/api/admin/role-requests/{roleRequest.Id}/review",
                new AdminReviewRequest("Approve", null, correction));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(code, (await response.Content.ReadFromJsonAsync<ApiError>())?.Code);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(RoleRequestStatus.Pending, (await db.RoleRequests.SingleAsync(item => item.Id == roleRequest.Id)).Status);
            Assert.False(await db.UserSpecialties.AnyAsync(item => item.UserId == roleRequest.UserId));
            Assert.Equal(chosen, RoleRequestSpecialtyIds(await db.RoleRequests.SingleAsync(item => item.Id == roleRequest.Id)));
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    private static IReadOnlyList<int> RoleRequestSpecialtyIds(RoleRequest request)
        => string.IsNullOrWhiteSpace(request.SpecialtyIds) ? [] : request.SpecialtyIds.Split(',').Select(int.Parse).ToList();

    private static async Task<(IReadOnlyList<int> Chosen, IReadOnlyList<int> Corrected, IReadOnlyDictionary<int, string> Names)>
        AddCertificationWithSpecialtiesAsync(RoleRequestApiFactory factory, AppRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var names = await db.CourseSubjects.ToDictionaryAsync(item => item.Id, item => item.Name);
        var ids = names.Keys.OrderBy(id => id).ToList();
        var chosen = ids.Take(3).ToList();
        var corrected = new List<int> { ids[1], ids[4] };
        var applicant = new User
        {
            Email = $"specialty.{Guid.NewGuid():N}@colearnx.test",
            PasswordHash = "hash",
            FullName = "Specialty Applicant",
            DisplayName = "Applicant",
        };
        db.Users.Add(applicant);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = applicant.Id, Role = role, CertifiedAt = null });
        db.RoleRequests.Add(new RoleRequest { UserId = applicant.Id, RequestedRole = role, SpecialtyIds = string.Join(',', chosen) });
        await db.SaveChangesAsync();
        return (chosen, corrected, names);
    }

    [Fact]
    public async Task Review_RejectsUnknownDecision()
    {
        var factory = new RoleRequestApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            await AddCertificationAsync(factory, AppRole.Creator);
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
            db.UserRoles.Add(new UserRole { UserId = user.Id, Role = AppRole.Trainer, CertifiedAt = null });
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
            _ = factory.Services;
            await AddCertificationAsync(factory, AppRole.Creator);
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

    private static async Task AddCertificationAsync(RoleRequestApiFactory factory, AppRole role)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var applicant = new User
        {
            Email = $"certification.{Guid.NewGuid():N}@colearnx.test",
            PasswordHash = "hash",
            FullName = "Certification Applicant",
            DisplayName = "Applicant",
        };
        db.Users.Add(applicant);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = applicant.Id, Role = role, CertifiedAt = null });
        db.RoleRequests.Add(new RoleRequest { UserId = applicant.Id, RequestedRole = role });
        await db.SaveChangesAsync();
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
