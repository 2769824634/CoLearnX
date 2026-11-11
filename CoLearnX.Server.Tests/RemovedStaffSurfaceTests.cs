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
    public async Task Staff_and_admin_staff_endpoints_are_not_exposed_but_history_schema_remains()
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

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.NotNull(db.Model.FindEntityType("CoLearnX.Server.Domain.Entities.StaffAccount"));
        Assert.NotNull(db.Model.FindEntityType("CoLearnX.Server.Domain.Entities.AuditLog")!
            .FindProperty("StaffAccountId"));
        _ = await db.StaffAccounts.CountAsync();
    }
}
