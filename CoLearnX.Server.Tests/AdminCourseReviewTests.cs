using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class AdminCourseReviewTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public AdminCourseReviewTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_course_queue_includes_interest_names_and_course_material_version_summary_without_storage_paths()
    {
        using var creator = await ApiClient.AsCreatorAsync(_factory);
        var leafInterestId = await GetLeafInterestIdAsync();
        var courseTitle = $"Admin summary {Guid.NewGuid():N}";
        var course = await CreateCourseAsync(creator, courseTitle, leafInterestId);
        var upload = await creator.PostAsync(
            "/api/materials",
            PdfForm($"Summary guide {Guid.NewGuid():N}", "summary.pdf", course.Id));
        upload.EnsureSuccessStatusCode();
        var material = await upload.Content.ReadFromJsonAsync<MaterialDto>(ApiJson.Options);
        Assert.NotNull(material);
        (await creator.PostAsync($"/api/creator/courses/{course.Id}/submit", null)).EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var firstVersion = await db.CourseMaterialVersions
                .SingleAsync(item => item.LearningMaterialId == material.Id);
            firstVersion.Status = MaterialVersionStatus.Approved;
            firstVersion.ReviewedAt = DateTime.UtcNow;
            db.CourseMaterialVersions.Add(new CourseMaterialVersion
            {
                LearningMaterialId = material.Id,
                VersionNumber = 2,
                FilePath = $"materials/{material.Id}/summary-v2.pdf",
                Format = "PDF",
                Status = MaterialVersionStatus.Rejected,
            });

            var unrelatedMaterial = new LearningMaterial
            {
                CreatorId = course.CreatorId,
                Title = $"Unrelated {Guid.NewGuid():N}",
                FilePath = $"materials/{course.CreatorId}/unrelated.pdf",
                Format = "PDF",
                Category = "Design",
                Status = MaterialStatus.PendingReview,
            };
            db.LearningMaterials.Add(unrelatedMaterial);
            await db.SaveChangesAsync();
            db.CourseMaterialVersions.Add(new CourseMaterialVersion
            {
                LearningMaterialId = unrelatedMaterial.Id,
                VersionNumber = 1,
                FilePath = unrelatedMaterial.FilePath,
                Format = "PDF",
                Status = MaterialVersionStatus.PendingApproval,
            });
            await db.SaveChangesAsync();
        }

        using var admin = await ApiClient.AsOperationsAdminAsync(_factory);
        var response = await admin.GetAsync($"/api/admin/courses?status={CourseStatus.PendingApproval}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        var item = payload.Single(node => node!["id"]!.GetValue<int>() == course.Id)!.AsObject();

        var interests = item["interests"]!.AsArray();
        Assert.Contains(interests, interest => interest!["name"]!.GetValue<string>()!.Length > 0);
        Assert.Equal(2, item["materialVersionCount"]!.GetValue<int>());
        Assert.Equal(1, item["materialVersionStatusCounts"]!["Approved"]!.GetValue<int>());
        Assert.Equal(1, item["materialVersionStatusCounts"]!["Rejected"]!.GetValue<int>());
        var versions = item["materialVersions"]!.AsArray();
        Assert.Equal(2, versions.Count);
        Assert.Contains(versions, version => version!["versionNumber"]!.GetValue<int>() == 1);
        Assert.Contains(versions, version => version!["versionNumber"]!.GetValue<int>() == 2);
        Assert.DoesNotContain("filePath", item.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("materials/", item.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_course_material_summary_is_scoped_to_the_course_link()
    {
        using var creator = await ApiClient.AsCreatorAsync(_factory);
        var leafInterestId = await GetLeafInterestIdAsync();
        var first = await CreateCourseAsync(creator, $"Linked course {Guid.NewGuid():N}", leafInterestId);
        var second = await CreateCourseAsync(creator, $"Other course {Guid.NewGuid():N}", leafInterestId);
        var upload = await creator.PostAsync(
            "/api/materials",
            PdfForm($"Only first course {Guid.NewGuid():N}", "first-only.pdf", first.Id));
        upload.EnsureSuccessStatusCode();
        (await creator.PostAsync($"/api/creator/courses/{first.Id}/submit", null)).EnsureSuccessStatusCode();
        (await creator.PostAsync($"/api/creator/courses/{second.Id}/submit", null)).EnsureSuccessStatusCode();

        using var admin = await ApiClient.AsOperationsAdminAsync(_factory);
        var response = await admin.GetAsync($"/api/admin/courses?status={CourseStatus.PendingApproval}");
        response.EnsureSuccessStatusCode();
        var payload = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        var secondNode = payload.Single(node => node!["id"]!.GetValue<int>() == second.Id)!.AsObject();
        Assert.Equal(0, secondNode["materialVersionCount"]!.GetValue<int>());
        Assert.Empty(secondNode["materialVersions"]!.AsArray());
    }

    private async Task<int> GetLeafInterestIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        return await db.Interests.Where(item => item.ParentId != null).Select(item => item.Id).FirstAsync();
    }

    private static async Task<CreatorCourseDto> CreateCourseAsync(HttpClient creator, string title, int interestId)
    {
        var response = await creator.PostAsJsonAsync("/api/creator/courses", new
        {
            code = $"ADM-{Guid.NewGuid():N}",
            title,
            description = "Course submitted for admin summary review.",
            courseLevelId = 1,
            learningPathId = 1,
            category = "Design",
            creditCost = 20,
            learningOutcomes = new[] { "Review course materials" },
            interestIds = new[] { interestId },
        }, ApiJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatorCourseDto>(ApiJson.Options))!;
    }

    private static MultipartFormDataContent PdfForm(string title, string fileName, int courseId)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        form.Add(new StringContent(courseId.ToString()), "courseId");
        form.Add(new StringContent("Design"), "category");
        var file = new ByteArrayContent("%PDF-1.4 summary"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return form;
    }
}
