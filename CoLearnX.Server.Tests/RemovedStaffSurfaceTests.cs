using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class RemovedStaffSurfaceTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory factory;

    public RemovedStaffSurfaceTests(CoLearnXApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Staff_endpoints_model_and_audit_actor_are_gone()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,
            (await anonymous.PostAsJsonAsync("/api/staff/auth/login", new { email = "removed@colearnx.test", password = "Password123!" })).StatusCode);

        using var admin = factory.CreateClient();
        var login = await admin.PostAsJsonAsync("/api/admin/auth/login",
            new AdminLoginRequest(SeedData.AdminEmail, SeedData.DemoPassword));
        login.EnsureSuccessStatusCode();
        var auth = await login.Content.ReadFromJsonAsync<AdminAuthResponse>();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/staff")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/audit-logs?actorType=Staff")).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.DoesNotContain(db.Model.GetEntityTypes(), type => type.ClrType.Name.Contains("Staff"));
        Assert.Null(db.Model.FindEntityType("CoLearnX.Server.Domain.Entities.AuditLog")!.FindProperty("StaffAccountId"));
    }
}
