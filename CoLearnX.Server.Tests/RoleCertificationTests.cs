using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

// Single-role sign-up and the certification gate for Trainer and Creator accounts.
public class RoleCertificationTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public RoleCertificationTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Member_sign_up_gets_only_a_certified_member_role()
    {
        var (email, user) = await RegisterAsync("Member");

        Assert.Equal(["Member"], user.Roles);
        Assert.Equal("Member", user.ActiveRole);
        Assert.NotNull(user.CertifiedAt);
        Assert.Equal(1, await RoleCountAsync(email));
    }

    [Theory]
    [InlineData("Trainer")]
    [InlineData("Creator")]
    public async Task Trainer_and_creator_sign_up_get_one_uncertified_role(string role)
    {
        var (email, user) = await RegisterAsync(role);

        Assert.Equal([role], user.Roles);
        Assert.Equal(role, user.ActiveRole);
        Assert.Null(user.CertifiedAt);
        Assert.Equal(1, await RoleCountAsync(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Admin")]
    [InlineData("2")]
    public async Task Sign_up_without_an_allowed_role_is_rejected(string? role)
    {
        var response = await ApiClient.Anonymous(_factory).PostAsJsonAsync("/api/auth/register",
            new RegisterRequest($"role.{Guid.NewGuid():N}@colearnx.test", SeedData.DemoPassword, "No Role", null, role), ApiJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ROLE_NOT_ALLOWED", (await ApiClient.ReadErrorAsync(response))?.Code);
    }

    [Fact]
    public async Task Uncertified_accounts_are_blocked_from_their_workspace_but_can_apply()
    {
        var (trainerEmail, _) = await RegisterAsync("Trainer");
        var (creatorEmail, _) = await RegisterAsync("Creator");
        using var trainer = await ApiClient.AsRoleAsync(_factory, trainerEmail, "Trainer");
        using var creator = await ApiClient.AsRoleAsync(_factory, creatorEmail, "Creator");

        await AssertCertificationRequiredAsync(await trainer.GetAsync("/api/trainer/course-invitations"));
        await AssertCertificationRequiredAsync(await creator.GetAsync("/api/creator/courses"));
        Assert.Equal(HttpStatusCode.OK, (await trainer.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await trainer.GetAsync("/api/role-requests/my")).StatusCode);

        var created = await trainer.PostAsync("/api/role-requests", await FormAsync("Trainer", 2));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Certification_needs_one_to_five_specialties_for_the_role_the_account_holds()
    {
        var (email, _) = await RegisterAsync("Trainer");
        using var trainer = await ApiClient.AsRoleAsync(_factory, email, "Trainer");

        await AssertErrorAsync(await trainer.PostAsync("/api/role-requests", await FormAsync("Trainer", 0)), HttpStatusCode.BadRequest, "SPECIALTIES_REQUIRED");
        await AssertErrorAsync(await trainer.PostAsync("/api/role-requests", await FormAsync("Trainer", 6)), HttpStatusCode.BadRequest, "TOO_MANY_SPECIALTIES");
        await AssertErrorAsync(await trainer.PostAsync("/api/role-requests", await FormAsync("Creator", 1)), HttpStatusCode.BadRequest, "ROLE_NOT_REQUESTABLE");
    }

    [Fact]
    public async Task Approval_certifies_the_existing_role_and_stores_the_specialties()
    {
        var (email, _) = await RegisterAsync("Trainer");
        using var trainer = await ApiClient.AsRoleAsync(_factory, email, "Trainer");
        var request = await (await trainer.PostAsync("/api/role-requests", await FormAsync("Trainer", 2)))
            .Content.ReadFromJsonAsync<RoleRequestDto>(ApiJson.Options);
        Assert.Equal(2, request!.Specialties!.Count);

        using var admin = await ApiClient.AsOperationsAdminAsync(_factory);
        (await admin.PostAsJsonAsync($"/api/admin/role-requests/{request.Id}/review",
            new AdminReviewRequest("Approve", "Verified."))).EnsureSuccessStatusCode();

        var me = await trainer.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.NotNull(me!.CertifiedAt);
        Assert.Equal(request.Specialties.Order(), me.Specialties!.Order());
        Assert.Equal(1, await RoleCountAsync(email));
        Assert.Equal(HttpStatusCode.OK, (await trainer.GetAsync("/api/trainer/course-invitations")).StatusCode);
        await AssertErrorAsync(await trainer.PostAsync("/api/role-requests", await FormAsync("Trainer", 1)), HttpStatusCode.Conflict, "ROLE_ALREADY_GRANTED");
    }

    [Fact]
    public async Task Rejection_keeps_the_role_uncertified_and_allows_a_new_application()
    {
        var (email, _) = await RegisterAsync("Creator");
        using var creator = await ApiClient.AsRoleAsync(_factory, email, "Creator");
        var request = await (await creator.PostAsync("/api/role-requests", await FormAsync("Creator", 1)))
            .Content.ReadFromJsonAsync<RoleRequestDto>(ApiJson.Options);

        using var admin = await ApiClient.AsOperationsAdminAsync(_factory);
        (await admin.PostAsJsonAsync($"/api/admin/role-requests/{request!.Id}/review",
            new AdminReviewRequest("Reject", "The identity document is unreadable."))).EnsureSuccessStatusCode();

        var me = await creator.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.Null(me!.CertifiedAt);
        Assert.Equal(HttpStatusCode.Created, (await creator.PostAsync("/api/role-requests", await FormAsync("Creator", 1))).StatusCode);
    }

    private async Task<(string Email, UserMeDto User)> RegisterAsync(string role)
    {
        var email = $"{role.ToLowerInvariant()}.{Guid.NewGuid():N}@colearnx.test";
        var response = await ApiClient.Anonymous(_factory).PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, SeedData.DemoPassword, $"New {role}", null, role), ApiJson.Options);
        response.EnsureSuccessStatusCode();
        await ApiClient.MarkEmailVerifiedAsync(_factory, email);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options);
        return (email, body!.User);
    }

    private async Task<int> RoleCountAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        return await db.UserRoles.CountAsync(role => role.User.Email == email);
    }

    private async Task<MultipartFormDataContent> FormAsync(string role, int specialtyCount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var ids = await db.CourseSubjects.OrderBy(item => item.Id).Select(item => item.Id).Take(specialtyCount).ToListAsync();
        var content = new MultipartFormDataContent
        {
            { new StringContent(role), "requestedRole" },
            { new StringContent("I have taught this subject for five years."), "statement" },
        };
        foreach (var id in ids) content.Add(new StringContent(id.ToString()), "specialtyIds");
        content.Add(PdfPart(), "resume", "resume.pdf");
        content.Add(PdfPart(), "idDocument", "id.pdf");
        return content;
    }

    private static ByteArrayContent PdfPart()
    {
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return file;
    }

    private static Task AssertCertificationRequiredAsync(HttpResponseMessage response)
        => AssertErrorAsync(response, HttpStatusCode.Forbidden, "CERTIFICATION_REQUIRED");

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await ApiClient.ReadErrorAsync(response))?.Code);
    }
}
