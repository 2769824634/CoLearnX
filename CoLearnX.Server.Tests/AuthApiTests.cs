using System.Net;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;

namespace CoLearnX.Server.Tests;

public class AuthApiTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public AuthApiTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_as_member_returns_token_and_active_role()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(SeedData.MemberEmail, SeedData.DemoPassword, "Member"),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal("Member", body.User.ActiveRole);
        Assert.Equal(SeedData.MemberEmail, body.User.Email);
        Assert.Contains("Member", body.User.Roles);
    }

    [Fact]
    public async Task Login_wrong_password_returns_LOGIN_FAILED()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(SeedData.MemberEmail, "wrong-password", "Member"),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal("LOGIN_FAILED", error.Code);
    }

    [Fact]
    public async Task Login_as_admin_without_admin_role_returns_LOGIN_FAILED()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(SeedData.MemberEmail, SeedData.DemoPassword, "Admin"),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal("LOGIN_FAILED", error.Code);
    }

    [Fact]
    public async Task Available_roles_for_member_returns_member_only()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/available-roles",
            new AvailableRolesRequest(SeedData.MemberEmail, SeedData.DemoPassword),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AvailableRolesDto>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.Equal(["Member"], body.Roles);
    }

    [Fact]
    public async Task Available_roles_for_trainer_returns_trainer_only()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/available-roles",
            new AvailableRolesRequest(SeedData.TrainerEmail, SeedData.DemoPassword),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AvailableRolesDto>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.Equal(["Trainer"], body.Roles);
    }

    [Fact]
    public async Task Available_roles_for_creator_returns_creator_only()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/available-roles",
            new AvailableRolesRequest(SeedData.CreatorEmail, SeedData.DemoPassword),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AvailableRolesDto>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.Equal(["Creator"], body.Roles);
    }

    [Fact]
    public async Task Available_roles_for_admin_uses_separate_identity_store()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/available-roles",
            new AvailableRolesRequest(SeedData.AdminEmail, SeedData.DemoPassword),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal("LOGIN_FAILED", error.Code);
        Assert.Contains("operations sign-in", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Available_roles_wrong_password_returns_LOGIN_FAILED()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/available-roles",
            new AvailableRolesRequest(SeedData.MemberEmail, "wrong-password"),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal("LOGIN_FAILED", error.Code);
    }

    [Fact]
    public async Task Me_without_token_returns_unauthorized()
    {
        var client = ApiClient.Anonymous(_factory);
        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
