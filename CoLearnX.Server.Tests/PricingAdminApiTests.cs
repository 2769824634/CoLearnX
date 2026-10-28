using System.Net;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class PricingAdminApiTests
{
    [Fact]
    public async Task Non_admin_is_rejected_on_every_pricing_endpoint()
    {
        await using var factory = new CoLearnXApiFactory();
        using var anonymous = ApiClient.Anonymous(factory);
        using var member = await ApiClient.AsMemberAsync(factory);

        foreach (var client in new[] { anonymous, member })
        {
            foreach (var (method, path) in Endpoints)
            {
                using var response = await client.SendAsync(new HttpRequestMessage(method, path));
                Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, $"{method} {path} returned {response.StatusCode}");
            }
        }
    }

    [Fact]
    public async Task Admin_can_list_prices_and_domain_changes_are_audited()
    {
        await using var factory = new CoLearnXApiFactory();
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);

        var tiers = await ReadAsync<List<TierRow>>(await admin.GetAsync("/api/admin/pricing/tiers"));
        Assert.Empty(tiers);
        var domains = await ReadAsync<List<DomainRow>>(await admin.GetAsync("/api/admin/pricing/domains"));
        Assert.Empty(domains);

        var pool = await ReadAsync<List<PoolRow>>(await admin.GetAsync("/api/admin/pricing/pool"));
        Assert.Equal(9, pool.Count);
        Assert.Equal(("Digital Technology", "Beginner", 40, 180), (pool[0].Subject, pool[0].LevelName, pool[0].MinTotalCredits, pool[0].MaxTotalCredits));
        Assert.Contains(pool, row => row.Subject == "Business and Management" && row.LevelName == "Advanced" && row.MinTotalCredits == 350 && row.MaxTotalCredits == 900);
        Assert.Contains(pool, row => row.Subject == "Humanities, Health and Creative" && row.LevelName == "Beginner" && row.MinTotalCredits == 20 && row.MaxTotalCredits == 120);
        Assert.All(pool, row => Assert.Equal(100, row.ContentSharePercent + row.DeliverySharePercent + row.PlatformSharePercent));
        Assert.All(pool, row => Assert.Null(row.MinHours));
    }

    private static readonly (HttpMethod Method, string Path)[] Endpoints =
    [
        (HttpMethod.Get, "/api/admin/pricing/tiers"),
        (HttpMethod.Get, "/api/admin/pricing/domains"),
        (HttpMethod.Post, "/api/admin/pricing/domains"),
        (HttpMethod.Put, "/api/admin/pricing/domains/1"),
        (HttpMethod.Post, "/api/admin/pricing/domains/1/deactivate"),
        (HttpMethod.Get, "/api/admin/pricing/pool"),
    ];

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(ApiJson.Options) ?? throw new InvalidOperationException("Empty response.");
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code, string? field)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options);
        Assert.NotNull(error);
        Assert.Equal(code, error.Code);
        if (field is null)
            Assert.True(error.FieldErrors is null || !error.FieldErrors.ContainsKey(field ?? ""));
        else
            Assert.True(error.FieldErrors != null && error.FieldErrors.ContainsKey(field));
    }

    private static async Task<List<AuditLog>> AuditAsync(CoLearnXApiFactory factory, string action)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        return await db.AuditLogs.AsNoTracking()
            .Where(log => log.Action == action && log.EntityType == "CourseDomain" && log.Result == "Success")
            .ToListAsync();
    }

    private sealed record TierRow(int Id, string Code, int TheoryMinHourlyCredits, int TheoryMaxHourlyCredits, int PracticeMinHourlyCredits, int PracticeMaxHourlyCredits, int SortOrder);
    private sealed record DomainRow(int Id, string Name, int PriceTierId, string TierCode, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
    private sealed record PoolRow(int Id, string SaleMode, int? PriceTierId, string? TierCode, string CourseType, int CourseLevelId, string LevelName, int? MinHourlyCredits, int? MaxHourlyCredits, int? MinHours, int? MaxHours, int? MinTotalCredits, int? MaxTotalCredits, int ContentSharePercent, int DeliverySharePercent, int PlatformSharePercent, string? Subject = null);
}
