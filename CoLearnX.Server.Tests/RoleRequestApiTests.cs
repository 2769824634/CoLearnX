using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class RoleRequestApiTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public RoleRequestApiTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task My_without_token_returns_unauthorized()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.GetAsync("/api/role-requests/my");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Json_create_without_files_returns_FILES_REQUIRED()
    {
        var applicant = await RegisterApplicantAsync($"nofile.{Guid.NewGuid():N}@colearnx.test", "Trainer");

        var response = await applicant.PostAsJsonAsync(
            "/api/role-requests",
            new CreateRoleRequest("Trainer"),
            ApiJson.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("FILES_REQUIRED", (await ApiClient.ReadErrorAsync(response))?.Code);
    }

    [Fact]
    public async Task Member_can_still_list_a_legacy_pending_creator_request()
    {
        var member = await ApiClient.AsMemberAsync(_factory);
        var me = await member.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            db.RoleRequests.Add(new RoleRequest
            {
                UserId = me!.Id,
                RequestedRole = AppRole.Creator,
                DegreeOrResumePath = "legacy-resume.pdf",
                IdDocumentPath = "legacy-id.pdf",
            });
            await db.SaveChangesAsync();
        }

        var mine = await member.GetFromJsonAsync<List<RoleRequestDto>>("/api/role-requests/my", ApiJson.Options);

        Assert.NotNull(mine);
        var creator = Assert.Single(mine, item => item.RequestedRole == "Creator");
        Assert.Equal("Pending", creator.Status);
        Assert.True(creator.HasResume);
        Assert.True(creator.HasIdDocument);
    }

    [Fact]
    public async Task Uncertified_trainer_can_apply_and_cannot_repeat_while_pending()
    {
        var applicant = await RegisterApplicantAsync($"role.{Guid.NewGuid():N}@colearnx.test", "Trainer");

        using var createdContent = ApplicationForm("Trainer");
        var created = await applicant.PostAsync("/api/role-requests", createdContent);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<RoleRequestDto>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.Equal("Trainer", body.RequestedRole);
        Assert.Equal("Pending", body.Status);
        Assert.True(body.HasResume);
        Assert.True(body.HasIdDocument);
        Assert.Equal("I run inclusive workshops.", body.Statement);

        var mine = await applicant.GetFromJsonAsync<List<RoleRequestDto>>("/api/role-requests/my", ApiJson.Options);
        Assert.Contains(mine!, item => item.Id == body.Id && item.Status == "Pending");

        using var duplicateContent = ApplicationForm("Trainer");
        var duplicate = await applicant.PostAsync("/api/role-requests", duplicateContent);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var error = await ApiClient.ReadErrorAsync(duplicate);
        Assert.Equal("ROLE_REQUEST_PENDING", error?.Code);
    }

    [Fact]
    public async Task Uploaded_files_are_stored_outside_the_materials_container()
    {
        var applicant = await RegisterApplicantAsync($"files.{Guid.NewGuid():N}@colearnx.test", "Creator");
        var me = await applicant.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.NotNull(me);

        using var content = ApplicationForm("Creator");
        var created = await applicant.PostAsync("/api/role-requests", content);
        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<RoleRequestDto>(ApiJson.Options);
        Assert.NotNull(body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var stored = await db.RoleRequests.SingleAsync(item => item.Id == body.Id);
        Assert.False(string.IsNullOrWhiteSpace(stored.DegreeOrResumePath));
        Assert.False(string.IsNullOrWhiteSpace(stored.IdDocumentPath));
        Assert.DoesNotContain("materials/", stored.DegreeOrResumePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materials/", stored.IdDocumentPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith($"{me.Id}/", stored.DegreeOrResumePath, StringComparison.Ordinal);
        Assert.StartsWith($"{me.Id}/", stored.IdDocumentPath, StringComparison.Ordinal);
        Assert.Equal("I run inclusive workshops.", stored.ApplicantStatement);

        var roleFiles = scope.ServiceProvider.GetRequiredService<IRoleRequestFileStorage>();
        var materials = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        Assert.NotNull(await roleFiles.OpenAsync(stored.DegreeOrResumePath));
        Assert.NotNull(await roleFiles.OpenAsync(stored.IdDocumentPath));
        Assert.Null(await materials.OpenAsync(stored.DegreeOrResumePath));
        Assert.Null(await materials.OpenAsync(stored.IdDocumentPath));
    }

    [Fact]
    public async Task Admin_can_download_uploaded_role_request_files()
    {
        var applicant = await RegisterApplicantAsync($"dl.{Guid.NewGuid():N}@colearnx.test", "Trainer");
        using var content = ApplicationForm("Trainer");
        var created = await applicant.PostAsync("/api/role-requests", content);
        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<RoleRequestDto>(ApiJson.Options);
        Assert.NotNull(body);

        var admin = await ApiClient.AsOperationsAdminAsync(_factory);
        var resume = await admin.GetAsync($"/api/admin/role-requests/{body.Id}/resume");
        var idDocument = await admin.GetAsync($"/api/admin/role-requests/{body.Id}/id-document");
        Assert.Equal(HttpStatusCode.OK, resume.StatusCode);
        Assert.Equal(HttpStatusCode.OK, idDocument.StatusCode);
        Assert.Equal("application/pdf", resume.Content.Headers.ContentType?.MediaType);

        var asApplicant = await applicant.GetAsync($"/api/admin/role-requests/{body.Id}/resume");
        Assert.Equal(HttpStatusCode.Unauthorized, asApplicant.StatusCode);
    }

    [Fact]
    public async Task Application_requires_a_statement_of_at_most_300_words()
    {
        var applicant = await RegisterApplicantAsync($"note.{Guid.NewGuid():N}@colearnx.test", "Trainer");

        using var missing = ApplicationForm("Trainer", statement: "  ");
        var missingResponse = await applicant.PostAsync("/api/role-requests", missing);
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
        Assert.Equal("STATEMENT_REQUIRED", (await ApiClient.ReadErrorAsync(missingResponse))?.Code);

        using var tooLong = ApplicationForm("Trainer", statement: string.Join(' ', Enumerable.Repeat("word", 301)));
        var tooLongResponse = await applicant.PostAsync("/api/role-requests", tooLong);
        Assert.Equal(HttpStatusCode.BadRequest, tooLongResponse.StatusCode);
        Assert.Equal("STATEMENT_TOO_LONG", (await ApiClient.ReadErrorAsync(tooLongResponse))?.Code);
    }

    [Fact]
    public async Task Member_cannot_request_member_or_a_role_already_held()
    {
        var member = await ApiClient.AsMemberAsync(_factory);
        var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");

        using var memberRoleContent = ApplicationForm("Member");
        var memberRole = await member.PostAsync("/api/role-requests", memberRoleContent);
        Assert.Equal(HttpStatusCode.BadRequest, memberRole.StatusCode);
        Assert.Equal("ROLE_NOT_REQUESTABLE", (await ApiClient.ReadErrorAsync(memberRole))?.Code);

        using var alreadyHeldContent = ApplicationForm("Trainer");
        var alreadyHeld = await trainer.PostAsync("/api/role-requests", alreadyHeldContent);
        Assert.Equal(HttpStatusCode.Conflict, alreadyHeld.StatusCode);
        Assert.Equal("ROLE_ALREADY_GRANTED", (await ApiClient.ReadErrorAsync(alreadyHeld))?.Code);
    }

    [Fact]
    public async Task Rejected_request_can_be_submitted_again_and_approval_certifies_the_role()
    {
        var email = $"grant.{Guid.NewGuid():N}@colearnx.test";
        var applicant = await RegisterApplicantAsync(email, "Creator");

        using var firstContent = ApplicationForm("Creator");
        var first = await applicant.PostAsync("/api/role-requests", firstContent);
        first.EnsureSuccessStatusCode();
        var pending = await first.Content.ReadFromJsonAsync<RoleRequestDto>(ApiJson.Options);
        Assert.NotNull(pending);

        var admin = await ApiClient.AsOperationsAdminAsync(_factory);
        var reject = await admin.PostAsJsonAsync(
            $"/api/admin/role-requests/{pending.Id}/review",
            new AdminReviewRequest("Reject", "Need a stronger portfolio."),
            ApiJson.Options);
        reject.EnsureSuccessStatusCode();

        using var retryContent = ApplicationForm("Creator");
        var retry = await applicant.PostAsync("/api/role-requests", retryContent);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var second = await retry.Content.ReadFromJsonAsync<RoleRequestDto>(ApiJson.Options);
        Assert.NotNull(second);
        Assert.NotEqual(pending.Id, second.Id);

        var approve = await admin.PostAsJsonAsync(
            $"/api/admin/role-requests/{second.Id}/review",
            new AdminReviewRequest("Approve", null),
            ApiJson.Options);
        approve.EnsureSuccessStatusCode();

        var mine = await applicant.GetFromJsonAsync<List<RoleRequestDto>>("/api/role-requests/my", ApiJson.Options);
        Assert.Contains(mine!, item => item.Id == second.Id && item.Status == "Approved");

        var me = await applicant.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.Contains("Creator", me!.Roles);
        Assert.Single(me.Roles);
        Assert.NotNull(me.CertifiedAt);

        using var againContent = ApplicationForm("Creator");
        var again = await applicant.PostAsync("/api/role-requests", againContent);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("ROLE_ALREADY_GRANTED", (await ApiClient.ReadErrorAsync(again))?.Code);
    }

    private async Task<HttpClient> RegisterApplicantAsync(string email, string role)
    {
        var client = ApiClient.Anonymous(_factory);
        var register = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(email, SeedData.DemoPassword, "Role Applicant", null, role),
            ApiJson.Options);
        register.EnsureSuccessStatusCode();
        await ApiClient.MarkEmailVerifiedAsync(_factory, email);
        return await ApiClient.AsRoleAsync(_factory, email, role);
    }

    [Fact]
    public async Task Applicant_can_upload_several_files_per_field_and_admin_downloads_each()
    {
        var applicant = await RegisterApplicantAsync($"multi.{Guid.NewGuid():N}@colearnx.test", "Trainer");
        using var content = ApplicationForm("Trainer", resumeCount: 2, idDocumentCount: 3);
        var created = await applicant.PostAsync("/api/role-requests", content);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<RoleRequestDto>(ApiJson.Options);
        Assert.NotNull(body);

        var admin = await ApiClient.AsOperationsAdminAsync(_factory);
        var queue = await admin.GetFromJsonAsync<List<AdminRoleRequestDto>>("/api/admin/role-requests", ApiJson.Options);
        var item = Assert.Single(queue!, request => request.Id == body.Id);
        Assert.Equal(2, item.ResumeFileCount);
        Assert.Equal(3, item.IdDocumentFileCount);

        var second = await admin.GetAsync($"/api/admin/role-requests/{body.Id}/resume?index=1");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("resume-2.pdf", second.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var third = await admin.GetAsync($"/api/admin/role-requests/{body.Id}/id-document?index=2");
        Assert.Equal(HttpStatusCode.OK, third.StatusCode);
        var missing = await admin.GetAsync($"/api/admin/role-requests/{body.Id}/id-document?index=3");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task More_than_five_files_in_one_field_returns_TOO_MANY_FILES()
    {
        var applicant = await RegisterApplicantAsync($"toomany.{Guid.NewGuid():N}@colearnx.test", "Trainer");
        using var content = ApplicationForm("Trainer", resumeCount: 6);
        var response = await applicant.PostAsync("/api/role-requests", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("TOO_MANY_FILES", (await ApiClient.ReadErrorAsync(response))?.Code);
    }

    [Fact]
    public async Task A_bad_file_type_in_the_second_field_saves_no_files()
    {
        var applicant = await RegisterApplicantAsync($"badtype.{Guid.NewGuid():N}@colearnx.test", "Trainer");
        var me = await applicant.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        using var content = ApplicationForm("Trainer", idDocumentName: "id.txt");
        var response = await applicant.PostAsync("/api/role-requests", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("INVALID_FILE_TYPE", (await ApiClient.ReadErrorAsync(response))?.Code);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.False(await db.RoleRequests.AnyAsync(item => item.UserId == me!.Id));
    }

    private MultipartFormDataContent ApplicationForm(
        string requestedRole,
        string statement = "I run inclusive workshops.",
        int resumeCount = 1,
        int idDocumentCount = 1,
        string idDocumentName = "id.pdf")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var specialtyId = db.CourseSubjects.OrderBy(item => item.Id).Select(item => item.Id).First();
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(requestedRole), "requestedRole");
        content.Add(new StringContent(statement), "statement");
        content.Add(new StringContent(specialtyId.ToString()), "specialtyIds");
        for (var i = 0; i < resumeCount; i++)
            content.Add(PdfPart(), "resume", $"resume-{i}.pdf");
        for (var i = 0; i < idDocumentCount; i++)
            content.Add(PdfPart(), "idDocument", idDocumentName);
        return content;
    }

    private static ByteArrayContent PdfPart()
    {
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return file;
    }
}
