using System.Net;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;

namespace CoLearnX.Server.Tests;

public class EnrollmentApiTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public EnrollmentApiTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Enrol_without_token_returns_unauthorized()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsJsonAsync(
            "/api/enrollments",
            new EnrolRequest(1, 1),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_returns_INSUFFICIENT_CREDITS_when_balance_too_low()
    {
        var client = ApiClient.Anonymous(_factory);
        var email = $"zero.{Guid.NewGuid():N}@colearnx.test";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, SeedData.DemoPassword, "Zero Balance", null),
            ApiJson.Options);
        register.EnsureSuccessStatusCode();

        var member = await ApiClient.AsMemberAsync(_factory, email);
        var (courseId, sessionId) = await FirstPublishedSessionAsync(member);

        var response = await member.PostAsJsonAsync(
            "/api/enrollments",
            new EnrolRequest(courseId, sessionId),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal("INSUFFICIENT_CREDITS", error.Code);
    }

    [Fact]
    public async Task Enrol_with_credits_returns_new_balance()
    {
        var member = await ApiClient.AsMemberAsync(_factory);
        var mine = await member.GetFromJsonAsync<List<EnrollmentDto>>("/api/enrollments/my", ApiJson.Options)
            ?? [];
        var activeIds = mine
            .Where(e => string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.CourseId)
            .ToHashSet();

        var catalog = await member.GetFromJsonAsync<List<CourseListItemDto>>("/api/courses", ApiJson.Options)
            ?? [];
        var target = catalog.First(c => !activeIds.Contains(c.Id));
        var detail = await member.GetFromJsonAsync<CourseDetailDto>($"/api/courses/{target.Id}", ApiJson.Options)
            ?? throw new InvalidOperationException("Course detail missing.");
        var session = detail.Sessions[0];
        var balanceBefore = (await member.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options))!.CreditBalance;

        var response = await member.PostAsJsonAsync(
            "/api/enrollments",
            new EnrolRequest(target.Id, session.Id),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<EnrolResultDto>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.Equal(target.CreditCost, body.CreditsSpent);
        Assert.Equal(balanceBefore - target.CreditCost, body.BalanceAfter);
        Assert.Equal(target.Code, body.CourseCode);
    }

    private static async Task<(int CourseId, int SessionId)> FirstPublishedSessionAsync(HttpClient client)
    {
        var catalog = await client.GetFromJsonAsync<List<CourseListItemDto>>("/api/courses", ApiJson.Options)
            ?? throw new InvalidOperationException("Catalog empty.");
        var detail = await client.GetFromJsonAsync<CourseDetailDto>($"/api/courses/{catalog[0].Id}", ApiJson.Options)
            ?? throw new InvalidOperationException("Course detail missing.");
        return (detail.Id, detail.Sessions[0].Id);
    }
}
