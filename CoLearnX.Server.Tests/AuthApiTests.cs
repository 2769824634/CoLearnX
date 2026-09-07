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
            new LoginRequest("huang.yousheng@colearnx.com", SeedData.DemoPassword, "Member"),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal("Member", body.User.ActiveRole);
        Assert.Equal("huang.yousheng@colearnx.com", body.User.Email);
        Assert.Contains("Member", body.User.Roles);
    }

    [Fact]
    public async Task Login_wrong_password_returns_LOGIN_FAILED()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("huang.yousheng@colearnx.com", "wrong-password", "Member"),
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
            new LoginRequest("huang.yousheng@colearnx.com", SeedData.DemoPassword, "Admin"),
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
