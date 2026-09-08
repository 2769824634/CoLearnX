using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;

namespace CoLearnX.Server.Tests;

internal static class ApiJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}

internal static class ApiClient
{
    public static HttpClient Anonymous(CoLearnXApiFactory factory)
        => factory.CreateClient();

    public static async Task<HttpClient> AsMemberAsync(
        CoLearnXApiFactory factory,
        string email = SeedData.MemberEmail,
        string password = SeedData.DemoPassword)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password, "Member"),
            ApiJson.Options);
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options)
            ?? throw new InvalidOperationException("Login returned no body.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        return client;
    }

    public static async Task<ApiError?> ReadErrorAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options);
}
