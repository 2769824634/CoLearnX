using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;

namespace CoLearnX.Server.Tests;

public class WebBaseBdCompatibilityTests(CoLearnXApiFactory factory) : IClassFixture<CoLearnXApiFactory>
{
    [Theory]
    [InlineData("Trainer", "/api/trainer/intakes")]
    [InlineData("Creator", "/api/creator/intake-applications")]
    public async Task Web_base_demo_accounts_can_enter_bd_workspaces(string role, string workspacePath)
    {
        var email = role == "Trainer" ? SeedData.TrainerEmail : SeedData.CreatorEmail;
        var client = factory.CreateClient();

        using var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, SeedData.DemoPassword, role),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options);
        Assert.NotNull(auth);
        Assert.Equal(role, auth.User.ActiveRole);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        using var workspace = await client.GetAsync(workspacePath);
        Assert.Equal(HttpStatusCode.OK, workspace.StatusCode);
    }

    [Fact]
    public async Task Web_base_admin_demo_account_uses_separate_admin_identity()
    {
        var client = factory.CreateClient();

        using var login = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new { email = SeedData.AdminEmail, password = SeedData.DemoPassword },
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var document = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = document.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var audit = await client.GetAsync("/api/admin/audit-logs?limit=10");
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);

        using var forbiddenIntakeReview = await client.GetAsync("/api/admin/intakes");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenIntakeReview.StatusCode);
    }
}
