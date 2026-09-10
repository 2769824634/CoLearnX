using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class MaterialApiTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public MaterialApiTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Upload_without_token_returns_unauthorized()
    {
        var client = ApiClient.Anonymous(_factory);

        var response = await client.PostAsync("/api/materials", PdfForm("Demo", "notes.pdf"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_as_member_returns_forbidden()
    {
        var client = await ApiClient.AsMemberAsync(_factory);

        var response = await client.PostAsync("/api/materials", PdfForm("Demo", "notes.pdf"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Upload_without_course_returns_course_required()
    {
        var creator = await ApiClient.AsCreatorAsync(_factory);

        var response = await creator.PostAsync("/api/materials", PdfForm("Orphan notes", "notes.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.Equal("COURSE_REQUIRED", error?.Code);
    }

    [Fact]
    public async Task Creator_upload_attaches_to_owned_course_and_filters_by_course()
    {
        var creator = await ApiClient.AsCreatorAsync(_factory);
        var courseId = await CreateOwnedCourseAsync(creator);
        var otherCourseId = await CreateOwnedCourseAsync(creator);
        var title = $"UI Notes {Guid.NewGuid():N}";

        var response = await creator.PostAsync("/api/materials", PdfForm(title, "ui-notes.pdf", courseId: courseId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<MaterialDto>(ApiJson.Options);
        Assert.NotNull(created);
        Assert.Equal(title, created.Title);
        Assert.Equal("PDF", created.Format);
        Assert.Equal("PendingReview", created.Status);
        Assert.Equal("Zou Ruiqi", created.CreatorName);
        Assert.Equal(courseId, created.CourseId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            Assert.True(await db.CourseMaterials.AnyAsync(link =>
                link.CourseId == courseId && link.LearningMaterialId == created.Id));
        }

        var forCourse = await creator.GetFromJsonAsync<List<MaterialDto>>($"/api/materials?courseId={courseId}", ApiJson.Options);
        Assert.Contains(forCourse!, m => m.Id == created.Id && m.CourseId == courseId);

        var otherList = await creator.GetFromJsonAsync<List<MaterialDto>>($"/api/materials?courseId={otherCourseId}", ApiJson.Options);
        Assert.DoesNotContain(otherList!, m => m.Id == created.Id);
    }

    [Fact]
    public async Task Creator_cannot_upload_to_another_creators_course()
    {
        var owner = await ApiClient.AsCreatorAsync(_factory);
        var courseId = await CreateOwnedCourseAsync(owner);

        var otherEmail = $"creator-{Guid.NewGuid():N}@example.com";
        await EnsureCreatorUserAsync(otherEmail);
        var other = await ApiClient.AsRoleAsync(_factory, otherEmail, "Creator");

        var response = await other.PostAsync("/api/materials", PdfForm("Stolen notes", "notes.pdf", courseId: courseId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.Equal("COURSE_NOT_FOUND", error?.Code);
    }

    [Fact]
    public async Task Creator_file_upload_appears_in_admin_pending_material_queue()
    {
        var creator = await ApiClient.AsCreatorAsync(_factory);
        var courseId = await CreateOwnedCourseAsync(creator);
        var title = $"Admin Queue {Guid.NewGuid():N}";
        var upload = await creator.PostAsync("/api/materials", PdfForm(title, "queue.pdf", courseId: courseId));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        var admin = await ApiClient.AsOperationsAdminAsync(_factory);
        var pending = await admin.GetFromJsonAsync<List<MaterialVersionDto>>(
            "/api/admin/material-versions?status=PendingApproval",
            ApiJson.Options);

        Assert.NotNull(pending);
        var queued = Assert.Single(pending, item => item.Title == title && item.Status == "PendingApproval" && item.CreatorName == "Zou Ruiqi");
        Assert.Equal(courseId, queued.CourseId);

        var download = await admin.GetAsync($"/api/admin/material-versions/{queued.VersionId}/file");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Member_cannot_download_pending_material()
    {
        var creator = await ApiClient.AsCreatorAsync(_factory);
        var courseId = await CreateOwnedCourseAsync(creator);
        var title = $"Shared {Guid.NewGuid():N}";
        var payload = "%PDF-1.4 shared-bytes"u8.ToArray();

        var upload = await creator.PostAsync("/api/materials", PdfForm(title, "shared.pdf", payload, courseId));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var created = await upload.Content.ReadFromJsonAsync<MaterialDto>(ApiJson.Options);
        Assert.NotNull(created);

        var own = await creator.GetAsync($"/api/materials/{created.Id}/file");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(payload, await own.Content.ReadAsByteArrayAsync());

        var member = await ApiClient.AsMemberAsync(_factory);
        var download = await member.GetAsync($"/api/materials/{created.Id}/file");
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_disallowed_extension()
    {
        var creator = await ApiClient.AsCreatorAsync(_factory);
        var courseId = await CreateOwnedCourseAsync(creator);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Bad file"), "title");
        content.Add(new StringContent(courseId.ToString()), "courseId");
        var file = new ByteArrayContent("MZ"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", "payload.exe");

        var response = await creator.PostAsync("/api/materials", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal("UPLOAD_FAILED", error.Code);
    }

    [Fact]
    public async Task Download_missing_material_returns_not_found()
    {
        var member = await ApiClient.AsMemberAsync(_factory);

        var response = await member.GetAsync("/api/materials/999999/file");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Storage_status_reports_local_in_test_host()
    {
        var creator = await ApiClient.AsCreatorAsync(_factory);

        var status = await creator.GetFromJsonAsync<StorageStatusDto>("/api/materials/storage", ApiJson.Options);

        Assert.NotNull(status);
        Assert.Equal("Local", status.Provider);
        Assert.False(status.CloudLinks);
    }

    [Fact]
    public async Task Cloud_link_on_local_storage_returns_unavailable()
    {
        var creator = await ApiClient.AsCreatorAsync(_factory);
        var courseId = await CreateOwnedCourseAsync(creator);
        var upload = await creator.PostAsync("/api/materials", PdfForm("Local only", "local.pdf", courseId: courseId));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var created = await upload.Content.ReadFromJsonAsync<MaterialDto>(ApiJson.Options);
        Assert.NotNull(created);

        var response = await creator.GetAsync($"/api/materials/{created.Id}/cloud-link");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal("CLOUD_LINK_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task Cloud_link_missing_material_returns_not_found()
    {
        var creator = await ApiClient.AsCreatorAsync(_factory);

        var response = await creator.GetAsync("/api/materials/999999/cloud-link");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<int> CreateOwnedCourseAsync(HttpClient creator)
    {
        var response = await creator.PostAsJsonAsync("/api/creator/courses", new
        {
            code = $"MAT-{Guid.NewGuid():N}",
            title = "Material host course",
            description = "Owns uploaded learning materials.",
            courseLevelId = 1,
            learningPathId = 1,
            category = "Design",
            creditCost = 20,
            learningOutcomes = new[] { "Attach materials to this course" },
        });
        response.EnsureSuccessStatusCode();
        var course = await response.Content.ReadFromJsonAsync<CreatorCourseDto>(ApiJson.Options);
        return course!.Id;
    }

    private async Task EnsureCreatorUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        if (await db.Users.AnyAsync(user => user.Email == email))
            return;

        var creator = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedData.DemoPassword),
            FullName = "Another Creator",
            DisplayName = "Other Creator",
        };
        creator.Roles.Add(new UserRole { Role = Domain.Enums.AppRole.Creator });
        db.Users.Add(creator);
        await db.SaveChangesAsync();
    }

    private static MultipartFormDataContent PdfForm(string title, string fileName, byte[]? payload = null, int? courseId = null)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(title), "title");
        content.Add(new StringContent("Design"), "category");
        if (courseId is int id)
            content.Add(new StringContent(id.ToString()), "courseId");
        var file = new ByteArrayContent(payload ?? "%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", fileName);
        return content;
    }
}
