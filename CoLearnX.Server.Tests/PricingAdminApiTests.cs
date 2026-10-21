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
        Assert.Equal(["A", "B", "C", "D", "E"], tiers.Select(tier => tier.Code).ToArray());
        var tierC = tiers.Single(tier => tier.Code == "C").Id;
        var tierA = tiers.Single(tier => tier.Code == "A").Id;

        var domains = await ReadAsync<List<DomainRow>>(await admin.GetAsync("/api/admin/pricing/domains"));
        Assert.Equal(8, domains.Count);
        Assert.Contains(domains, domain => domain.Name == "IT and Data" && domain.TierCode == "B" && domain.IsActive);
        Assert.Equal(["A", "A", "B"], domains.Take(3).Select(domain => domain.TierCode).ToArray());

        var pool = await ReadAsync<List<PoolRow>>(await admin.GetAsync("/api/admin/pricing/pool"));
        Assert.Equal(33, pool.Count);
        Assert.Equal(("Intake", "A", "Theory", "Beginner", 40, 80, 2, 12), (pool[0].SaleMode, pool[0].TierCode, pool[0].CourseType, pool[0].LevelName, pool[0].MinHourlyCredits, pool[0].MaxHourlyCredits, pool[0].MinHours, pool[0].MaxHours));
        Assert.Equal(30, pool.Count(row => row.SaleMode == "Intake"));
        Assert.Equal(3, pool.Count(row => row.SaleMode == "CoursePack" && row.TierCode == null && row.MinTotalCredits != null));
        Assert.All(pool, row => Assert.Equal(100, row.ContentSharePercent + row.DeliverySharePercent + row.PlatformSharePercent));

        var createdResponse = await admin.PostAsJsonAsync("/api/admin/pricing/domains", new { name = " Household Finance ", priceTierId = tierC }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<DomainRow>(ApiJson.Options);
        Assert.NotNull(created);
        Assert.Equal("Household Finance", created.Name);
        Assert.Equal("C", created.TierCode);
        Assert.True(created.IsActive);
        var createdAudit = await AuditAsync(factory, "CourseDomainCreated");
        Assert.Equal($"Name: Household Finance; Tier: C", createdAudit.Single().Reason);

        var updatedResponse = await admin.PutAsJsonAsync($"/api/admin/pricing/domains/{created.Id}", new { name = "Home Finance", priceTierId = tierA }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<DomainRow>(ApiJson.Options);
        Assert.NotNull(updated);
        Assert.Equal("Home Finance", updated.Name);
        Assert.Equal("A", updated.TierCode);
        var updatedAudit = await AuditAsync(factory, "CourseDomainUpdated");
        Assert.Equal("Name: Household Finance -> Home Finance; Tier: C -> A", updatedAudit.Single().Reason);

        var deactivatedResponse = await admin.PostAsync($"/api/admin/pricing/domains/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactivatedResponse.StatusCode);
        var deactivated = await deactivatedResponse.Content.ReadFromJsonAsync<DomainRow>(ApiJson.Options);
        Assert.NotNull(deactivated);
        Assert.False(deactivated.IsActive);
        var deactivatedAudit = await AuditAsync(factory, "CourseDomainDeactivated");
        Assert.Equal("Name: Home Finance", deactivatedAudit.Single().Reason);

        var listed = await ReadAsync<List<DomainRow>>(await admin.GetAsync("/api/admin/pricing/domains"));
        Assert.Contains(listed, domain => domain.Id == created.Id && domain.IsActive == false);

        var beforeInactiveEdit = (await AuditAsync(factory, "CourseDomainUpdated")).Count;
        var inactiveEdit = await admin.PutAsJsonAsync($"/api/admin/pricing/domains/{created.Id}", new { name = "Home Finance Again", priceTierId = tierA }, ApiJson.Options);
        await AssertError(inactiveEdit, HttpStatusCode.Conflict, "DOMAIN_INACTIVE", null);
        Assert.Equal(beforeInactiveEdit, (await AuditAsync(factory, "CourseDomainUpdated")).Count);

        var inactiveDeactivate = await admin.PostAsync($"/api/admin/pricing/domains/{created.Id}/deactivate", null);
        await AssertError(inactiveDeactivate, HttpStatusCode.Conflict, "DOMAIN_INACTIVE", null);
        Assert.Single(await AuditAsync(factory, "CourseDomainDeactivated"));

        var duplicate = await admin.PostAsJsonAsync("/api/admin/pricing/domains", new { name = "health and care", priceTierId = tierC }, ApiJson.Options);
        await AssertError(duplicate, HttpStatusCode.Conflict, "DOMAIN_NAME_TAKEN", "name");
        Assert.Single(await AuditAsync(factory, "CourseDomainCreated"));

        var unknownTier = await admin.PostAsJsonAsync("/api/admin/pricing/domains", new { name = "New Domain", priceTierId = 999999 }, ApiJson.Options);
        await AssertError(unknownTier, HttpStatusCode.BadRequest, "PRICE_TIER_NOT_FOUND", "priceTierId");

        var emptyName = await admin.PostAsJsonAsync("/api/admin/pricing/domains", new { name = "   ", priceTierId = tierC }, ApiJson.Options);
        await AssertError(emptyName, HttpStatusCode.BadRequest, "DOMAIN_NAME_INVALID", "name");

        var longName = await admin.PostAsJsonAsync("/api/admin/pricing/domains", new { name = new string('x', 81), priceTierId = tierC }, ApiJson.Options);
        await AssertError(longName, HttpStatusCode.BadRequest, "DOMAIN_NAME_INVALID", "name");

        var missing = await admin.PutAsJsonAsync("/api/admin/pricing/domains/999999", new { name = "Missing", priceTierId = tierA }, ApiJson.Options);
        await AssertError(missing, HttpStatusCode.NotFound, "DOMAIN_NOT_FOUND", null);
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
    private sealed record PoolRow(int Id, string SaleMode, int? PriceTierId, string? TierCode, string CourseType, int CourseLevelId, string LevelName, int? MinHourlyCredits, int? MaxHourlyCredits, int? MinHours, int? MaxHours, int? MinTotalCredits, int? MaxTotalCredits, int ContentSharePercent, int DeliverySharePercent, int PlatformSharePercent);
}
