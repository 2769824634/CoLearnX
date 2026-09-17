using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

    [Fact]
    public async Task Trainer_can_update_profile_headline_and_see_it_on_me()
    {
        var client = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        var me = await client.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.NotNull(me);
        Assert.Equal("Senior Trainer", me.TrainerHeadline);

        using var response = await client.PutAsJsonAsync(
            $"/api/users/{me.Id}",
            new UpdateProfileRequest(
                "Gu Yincheng",
                "Yincheng",
                "0411222333",
                "Workshop facilitator",
                null,
                null,
                null,
                new Dictionary<string, bool> { ["trainer"] = true },
                "UI/UX Design",
                "Lead workshop trainer"),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserMeDto>(ApiJson.Options);
        Assert.NotNull(updated);
        Assert.Equal("Gu Yincheng", updated.FullName);
        Assert.Equal("Lead workshop trainer", updated.TrainerHeadline);
        Assert.Equal("UI/UX Design", updated.Specialisations);
        Assert.Equal("0411222333", updated.Phone);
    }

    [Fact]
    public async Task Creator_can_update_expertise_and_headline()
    {
        var client = await ApiClient.AsCreatorAsync(_factory);
        var me = await client.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.NotNull(me);

        using var response = await client.PutAsJsonAsync(
            $"/api/users/{me.Id}",
            new UpdateProfileRequest(
                null,
                null,
                null,
                "Design materials author",
                null,
                null,
                null,
                null,
                null,
                null,
                "Design, Accessibility",
                "Course author"),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserMeDto>(ApiJson.Options);
        Assert.NotNull(updated);
        Assert.Equal("Course author", updated.CreatorHeadline);
        Assert.Equal("Design, Accessibility", updated.ExpertiseTags);
        Assert.Equal("Design materials author", updated.Bio);
    }

    [Fact]
    public async Task Register_creates_member_and_hashes_password()
    {
        var client = ApiClient.Anonymous(_factory);
        var email = $"new.{Guid.NewGuid():N}@colearnx.test";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, SeedData.DemoPassword, "New Learner", null),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.Equal("Member", body.User.ActiveRole);
        Assert.Equal(email, body.User.Email);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var stored = await db.Users.SingleAsync(user => user.Email == email);
        Assert.NotEqual(SeedData.DemoPassword, stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(SeedData.DemoPassword, stored.PasswordHash));
        Assert.NotEqual(Guid.Empty, stored.SessionStamp);
    }

    [Fact]
    public async Task Second_login_invalidates_the_previous_token()
    {
        var first = ApiClient.Anonymous(_factory);
        var firstLogin = await first.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(SeedData.MemberEmail, SeedData.DemoPassword, "Member"),
            ApiJson.Options);
        firstLogin.EnsureSuccessStatusCode();
        var firstBody = await firstLogin.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options);
        Assert.NotNull(firstBody);
        first.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstBody.AccessToken);

        var firstMe = await first.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, firstMe.StatusCode);

        var second = ApiClient.Anonymous(_factory);
        var secondLogin = await second.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(SeedData.MemberEmail, SeedData.DemoPassword, "Member"),
            ApiJson.Options);
        secondLogin.EnsureSuccessStatusCode();
        var secondBody = await secondLogin.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options);
        Assert.NotNull(secondBody);
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondBody.AccessToken);

        var superseded = await first.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, superseded.StatusCode);

        var currentMe = await second.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, currentMe.StatusCode);
    }

    [Fact]
    public async Task Second_admin_login_invalidates_the_previous_token()
    {
        var first = ApiClient.Anonymous(_factory);
        var firstLogin = await first.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(SeedData.AdminEmail, SeedData.DemoPassword),
            ApiJson.Options);
        firstLogin.EnsureSuccessStatusCode();
        var firstBody = await firstLogin.Content.ReadFromJsonAsync<AdminAuthResponse>(ApiJson.Options);
        Assert.NotNull(firstBody);
        first.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstBody.AccessToken);

        var firstMe = await first.GetAsync("/api/admin/auth/me");
        Assert.Equal(HttpStatusCode.OK, firstMe.StatusCode);

        var second = ApiClient.Anonymous(_factory);
        var secondLogin = await second.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(SeedData.AdminEmail, SeedData.DemoPassword),
            ApiJson.Options);
        secondLogin.EnsureSuccessStatusCode();
        var secondBody = await secondLogin.Content.ReadFromJsonAsync<AdminAuthResponse>(ApiJson.Options);
        Assert.NotNull(secondBody);
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondBody.AccessToken);

        var superseded = await first.GetAsync("/api/admin/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, superseded.StatusCode);

        var currentMe = await second.GetAsync("/api/admin/auth/me");
        Assert.Equal(HttpStatusCode.OK, currentMe.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_weak_password()
    {
        var client = ApiClient.Anonymous(_factory);
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"weak.{Guid.NewGuid():N}@colearnx.test", "password", "Weak User", null),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_does_not_reveal_existing_emails()
    {
        var client = ApiClient.Anonymous(_factory);
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(SeedData.MemberEmail, SeedData.DemoPassword, "Duplicate", null),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.Equal("REGISTER_FAILED", error?.Code);
        Assert.DoesNotContain("already", error?.Message ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_rejects_admin_email()
    {
        var client = ApiClient.Anonymous(_factory);
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(SeedData.AdminEmail, SeedData.DemoPassword, "Not Admin", null),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.Equal("REGISTER_FAILED", error?.Code);
    }

    [Fact]
    public async Task Register_rejects_password_that_contains_the_email_name()
    {
        var client = ApiClient.Anonymous(_factory);
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest("learner@colearnx.test", "Learner123!", "Learner", null),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.Equal("WEAK_PASSWORD", error?.Code);
    }
}
