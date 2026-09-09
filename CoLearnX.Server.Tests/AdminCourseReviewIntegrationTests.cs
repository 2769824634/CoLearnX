using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace CoLearnX.Server.Tests;

public class AdminCourseReviewIntegrationTests
{
    [Fact]
    public async Task Approve_PublishesAndAuditsExactlyOnce_WhenReplayed()
    {
        var factory = new CourseReviewApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var course = await FindPendingCourseAsync(client);

            var firstResponse = await client.PostAsJsonAsync(
                $"/api/admin/courses/{course.Id}/review",
                new AdminReviewRequest("Approve", "Ready for the published catalogue."));
            firstResponse.EnsureSuccessStatusCode();
            var firstResult = await firstResponse.Content.ReadFromJsonAsync<AdminCourseReviewResultDto>();

            var replayResponse = await client.PostAsJsonAsync(
                $"/api/admin/courses/{course.Id}/review",
                new AdminReviewRequest("Approve", null));
            replayResponse.EnsureSuccessStatusCode();
            var replayResult = await replayResponse.Content.ReadFromJsonAsync<AdminCourseReviewResultDto>();

            Assert.NotNull(firstResult);
            Assert.False(firstResult.AlreadyReviewed);
            Assert.Equal("Published", firstResult.Course.Status);
            Assert.NotNull(firstResult.Course.ReviewedAt);
            Assert.NotNull(replayResult);
            Assert.True(replayResult.AlreadyReviewed);

            using var publicClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });
            var catalogueResponse = await publicClient.GetAsync($"/api/courses/{course.Id}");
            Assert.Equal(HttpStatusCode.OK, catalogueResponse.StatusCode);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(CourseStatus.Published, (await db.Courses.FindAsync(course.Id))?.Status);
            Assert.Equal(1, await db.AuditLogs.CountAsync(log =>
                log.Action == "CoursePublished" && log.EntityId == course.Id.ToString()));
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task Reject_RequiresReason_AndLeavesCoursePending()
    {
        var factory = new CourseReviewApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var course = await FindPendingCourseAsync(client);

            var response = await client.PostAsJsonAsync(
                $"/api/admin/courses/{course.Id}/review",
                new AdminReviewRequest("Reject", " "));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal("REJECTION_REASON_REQUIRED", error?.Code);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(CourseStatus.PendingApproval, (await db.Courses.FindAsync(course.Id))?.Status);
            Assert.Equal(0, await db.AuditLogs.CountAsync(log =>
                (log.Action == "CoursePublished" || log.Action == "CourseRejected")
                && log.EntityId == course.Id.ToString()));
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
        var factory = new CourseReviewApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var course = await FindPendingCourseAsync(client);

            var firstResponse = await client.PostAsJsonAsync(
                $"/api/admin/courses/{course.Id}/review",
                new AdminReviewRequest("Reject", "Learning outcomes need clearer assessment criteria."));
            firstResponse.EnsureSuccessStatusCode();

            var replayResponse = await client.PostAsJsonAsync(
                $"/api/admin/courses/{course.Id}/review",
                new AdminReviewRequest("Reject", null));
            replayResponse.EnsureSuccessStatusCode();
            var replayResult = await replayResponse.Content.ReadFromJsonAsync<AdminCourseReviewResultDto>();
            Assert.True(replayResult?.AlreadyReviewed);

            var conflictResponse = await client.PostAsJsonAsync(
                $"/api/admin/courses/{course.Id}/review",
                new AdminReviewRequest("Approve", null));
            Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
            var error = await conflictResponse.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal("COURSE_NOT_PENDING_APPROVAL", error?.Code);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.Equal(CourseStatus.Rejected, (await db.Courses.FindAsync(course.Id))?.Status);
            Assert.Equal(1, await db.AuditLogs.CountAsync(log =>
                log.Action == "CourseRejected" && log.EntityId == course.Id.ToString()));
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task Review_RejectsPublishedCourseWithoutAdminReviewAudit()
    {
        var factory = new CourseReviewApiFactory();
        try
        {
            using var client = await CreateAdminClientAsync(factory);
            var publishedCourses = await client.GetFromJsonAsync<List<AdminCourseDto>>(
                "/api/admin/courses?status=Published");
            var course = publishedCourses!.First(item => item.ReviewedAt is null);

            var response = await client.PostAsJsonAsync(
                $"/api/admin/courses/{course.Id}/review",
                new AdminReviewRequest("Approve", null));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            Assert.Equal("COURSE_NOT_PENDING_APPROVAL", error?.Code);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task CourseReviewEndpoints_RequireAdminAuthentication()
    {
        var factory = new CourseReviewApiFactory();
        try
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });

            var response = await client.GetAsync("/api/admin/courses?status=PendingApproval");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public async Task PendingCourse_IsNotExposedByPublicCatalogueDetail()
    {
        var factory = new CourseReviewApiFactory();
        try
        {
            _ = factory.Services;
            int courseId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
                courseId = await db.Courses
                    .Where(course => course.Status == CourseStatus.PendingApproval)
                    .Select(course => course.Id)
                    .SingleAsync();
            }
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });

            var response = await client.GetAsync($"/api/courses/{courseId}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    [Fact]
    public void AdminApi_DoesNotExposeIntakeReviewRoute()
    {
        var factory = new CourseReviewApiFactory();
        try
        {
            _ = factory.Services;
            var routes = factory.Services
                .GetRequiredService<EndpointDataSource>()
                .Endpoints
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty);

            Assert.DoesNotContain(routes, route =>
                route.Contains("api/admin/intakes", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            factory.Dispose();
            factory.DeleteDatabase();
        }
    }

    private static async Task<HttpClient> CreateAdminClientAsync(CourseReviewApiFactory factory)
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

    private static async Task<AdminCourseDto> FindPendingCourseAsync(HttpClient client)
    {
        var courses = await client.GetFromJsonAsync<List<AdminCourseDto>>(
            "/api/admin/courses?status=PendingApproval");
        return Assert.Single(courses!);
    }

    private sealed class CourseReviewApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"colearnx-d3-{Guid.NewGuid():N}.db");

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
