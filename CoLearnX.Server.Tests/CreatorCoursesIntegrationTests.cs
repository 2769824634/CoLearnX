using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class CreatorCoursesIntegrationTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public CreatorCoursesIntegrationTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_ReturnsCreatorOwnedDraftCourse()
    {
        using var client = await AsCreatorAsync();
        var code = $"CRT-{Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/creator/courses", new
        {
            code,
            title = "Designing Accessible Learning",
            description = "Build inclusive learning experiences.",
            courseLevelId = 1,
            learningPathId = 1,
            category = "Design",
            creditCost = 24,
            learningOutcomes = new[] { "Identify accessibility barriers", "Apply inclusive patterns" },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var course = body.RootElement;
        Assert.Equal(code, course.GetProperty("code").GetString());
        Assert.Equal("Draft", course.GetProperty("status").GetString());
        Assert.Equal(SeedData.CreatorEmail, course.GetProperty("creatorEmail").GetString());
        Assert.Equal(1, course.GetProperty("courseLevelId").GetInt32());
        Assert.Equal(1, course.GetProperty("learningPathId").GetInt32());
        Assert.Equal(2, course.GetProperty("learningOutcomes").GetArrayLength());
        Assert.Equal($"/api/creator/courses/{course.GetProperty("id").GetInt32()}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task DraftCourse_CanBeListedEditedAndSubmitted_ThenBecomesLocked()
    {
        using var client = await AsCreatorAsync();
        var created = await CreateCourseAsync(client);
        var courseId = created["id"]!.GetValue<int>();
        var code = created["code"]!.GetValue<string>();

        var list = await client.GetFromJsonAsync<JsonArray>("/api/creator/courses");
        Assert.Contains(list!, item => item!["id"]!.GetValue<int>() == courseId);

        var detail = await client.GetFromJsonAsync<JsonObject>($"/api/creator/courses/{courseId}");
        Assert.Equal(SeedData.CreatorEmail, detail!["creatorEmail"]!.GetValue<string>());

        var update = new
        {
            code,
            title = "Updated Course Title",
            description = "Updated description",
            courseLevelId = 2,
            learningPathId = 2,
            category = "Technology",
            creditCost = 31,
            learningOutcomes = new[] { "Apply the updated method" },
        };
        var updateResponse = await client.PutAsJsonAsync($"/api/creator/courses/{courseId}", update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = JsonNode.Parse(await updateResponse.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("Updated Course Title", updated["title"]!.GetValue<string>());
        Assert.Equal("Intermediate", updated["courseLevelName"]!.GetValue<string>());

        var submitResponse = await client.PostAsync($"/api/creator/courses/{courseId}/submit", null);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submitted = JsonNode.Parse(await submitResponse.Content.ReadAsStringAsync())!.AsObject();
        Assert.Equal("PendingApproval", submitted["status"]!.GetValue<string>());

        var lockedResponse = await client.PutAsJsonAsync($"/api/creator/courses/{courseId}", update);
        Assert.Equal(HttpStatusCode.Conflict, lockedResponse.StatusCode);
        var error = await lockedResponse.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options);
        Assert.Equal("COURSE_NOT_EDITABLE", error?.Code);
    }

    [Fact]
    public async Task Create_RejectsDuplicateCodeAndWhitespaceFields()
    {
        using var client = await AsCreatorAsync();
        var created = await CreateCourseAsync(client);
        var duplicateCode = created["code"]!.GetValue<string>().ToLowerInvariant();

        var duplicate = await client.PostAsJsonAsync("/api/creator/courses", CourseRequest(duplicateCode));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("COURSE_CODE_EXISTS", (await duplicate.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options))?.Code);

        var invalid = await client.PostAsJsonAsync("/api/creator/courses", new
        {
            code = $"CRT-{Guid.NewGuid():N}",
            title = "   ",
            description = "Invalid fixture",
            courseLevelId = 1,
            learningPathId = 1,
            category = " ",
            creditCost = 20,
            learningOutcomes = Array.Empty<string>(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var error = await invalid.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options);
        Assert.Equal("INVALID_REQUEST", error?.Code);
        Assert.Contains("Title", error!.FieldErrors!.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Category", error.FieldErrors.Keys, StringComparer.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(1, await db.Courses.CountAsync(course => course.Code == duplicateCode));
    }

    [Fact]
    public async Task CreatorCourseRoutes_EnforceRoleAndOwnership()
    {
        using var owner = await AsCreatorAsync();
        var course = await CreateCourseAsync(owner);
        var courseId = course["id"]!.GetValue<int>();
        var otherEmail = $"creator-{Guid.NewGuid():N}@example.com";
        await AddCreatorAsync(otherEmail);

        using var other = await AsUserAsync(otherEmail, "Creator");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/creator/courses/{courseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PutAsJsonAsync($"/api/creator/courses/{courseId}", CourseRequest(course["code"]!.GetValue<string>()))).StatusCode);

        using var member = await AsUserAsync(SeedData.MemberEmail, "Member");
        var forbidden = await member.PostAsJsonAsync("/api/creator/courses", CourseRequest($"CRT-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("CREATOR_REQUIRED", (await forbidden.Content.ReadFromJsonAsync<ApiError>(ApiJson.Options))?.Code);
    }

    [Fact]
    public async Task Options_ReturnsCourseLevelsAndLearningPathsForTheForm()
    {
        using var client = await AsCreatorAsync();

        var response = await client.GetAsync("/api/creator/courses/options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        Assert.Contains(options["courseLevels"]!.AsArray(), item => item!["name"]!.GetValue<string>() == "Beginner");
        Assert.Contains(options["learningPaths"]!.AsArray(), item => item!["name"]!.GetValue<string>() == "Technology");
    }

    [Fact]
    public async Task SubmittedCourse_FlowsThroughAdminApprovalIntoTrainerIntakeCreation()
    {
        using var creator = await AsCreatorAsync();
        var course = await CreateCourseAsync(creator);
        var courseId = course["id"]!.GetValue<int>();

        using var materialContent = new MultipartFormDataContent();
        materialContent.Add(new StringContent("Kickoff slides"), "title");
        materialContent.Add(new StringContent("Design"), "category");
        materialContent.Add(new StringContent(courseId.ToString()), "courseId");
        var file = new ByteArrayContent("%PDF-1.4 course-pack"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        materialContent.Add(file, "file", "kickoff.pdf");
        var upload = await creator.PostAsync("/api/materials", materialContent);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var material = await upload.Content.ReadFromJsonAsync<MaterialDto>(ApiJson.Options);
        Assert.Equal(courseId, material?.CourseId);

        (await creator.PostAsync($"/api/creator/courses/{courseId}/submit", null)).EnsureSuccessStatusCode();

        using var admin = await AsAdminAsync();
        var pending = await admin.GetFromJsonAsync<List<AdminCourseDto>>("/api/admin/courses?status=PendingApproval", ApiJson.Options);
        Assert.Contains(pending!, item => item.Id == courseId && item.SubmittedByEmail == SeedData.CreatorEmail);
        var approval = await admin.PostAsJsonAsync(
            $"/api/admin/courses/{courseId}/review",
            new AdminReviewRequest("Approve", "Course core integration verified."),
            ApiJson.Options);
        approval.EnsureSuccessStatusCode();

        using var publicClient = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync($"/api/courses/{courseId}")).StatusCode);

        using var trainer = await AsUserAsync(SeedData.TrainerEmail, "Trainer");
        var start = DateTime.UtcNow.AddDays(14);
        var intakeCreate = await trainer.PostAsJsonAsync($"/api/trainer/courses/{courseId}/intakes", new
        {
            registrationOpensAt = DateTime.UtcNow.AddDays(1),
            registrationClosesAt = start.AddDays(-1),
            startsAt = start,
            endsAt = start.AddDays(2),
        });
        Assert.Equal(HttpStatusCode.Created, intakeCreate.StatusCode);
        var intake = await intakeCreate.Content.ReadFromJsonAsync<CourseIntakeDetailDto>(ApiJson.Options);
        Assert.NotNull(intake);

        var withSession = await trainer.PostAsJsonAsync($"/api/trainer/intakes/{intake.Id}/sessions",
            new CreateCourseSessionRequest("Workshop 1", start, start.AddHours(2), null,
                "Room 101, CoLearnX Campus", 30, start.AddHours(-1), intake.Version));
        withSession.EnsureSuccessStatusCode();
        intake = await withSession.Content.ReadFromJsonAsync<CourseIntakeDetailDto>(ApiJson.Options);
        Assert.NotNull(intake);

        var submitted = await trainer.PostAsJsonAsync($"/api/trainer/intakes/{intake.Id}/submit",
            new SubmitCourseIntakeRequest(intake.Version));
        submitted.EnsureSuccessStatusCode();
        intake = await submitted.Content.ReadFromJsonAsync<CourseIntakeDetailDto>(ApiJson.Options);
        Assert.NotNull(intake);

        var confirmed = await creator.PostAsJsonAsync($"/api/creator/intake-applications/{intake.Id}/review",
            new ReviewIntakeApplicationRequest("Confirm", null, intake.Version));
        confirmed.EnsureSuccessStatusCode();

        using var member = await AsUserAsync(SeedData.MemberEmail, "Member");
        var catalog = await member.GetFromJsonAsync<List<CourseListItemDto>>(
            $"/api/courses?search={Uri.EscapeDataString(course["title"]!.GetValue<string>())}",
            ApiJson.Options);
        Assert.Contains(catalog!, item => item.Id == courseId);

        var detail = await member.GetFromJsonAsync<CourseDetailDto>($"/api/courses/{courseId}", ApiJson.Options);
        Assert.NotNull(detail);
        var session = Assert.Single(detail.Sessions);
        var enrol = await member.PostAsJsonAsync("/api/enrollments", new EnrolRequest(courseId, session.Id), ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, enrol.StatusCode);
    }

    private async Task<HttpClient> AsCreatorAsync()
        => await AsUserAsync(SeedData.CreatorEmail, "Creator");

    private async Task<HttpClient> AsUserAsync(string email, string role)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, SeedData.DemoPassword, role),
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<AuthResponse>(ApiJson.Options)
            ?? throw new InvalidOperationException("Creator login returned no body.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    private async Task AddCreatorAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var creator = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(SeedData.DemoPassword),
            FullName = "Another Creator",
            DisplayName = "Other Creator",
        };
        creator.Roles.Add(new UserRole { Role = AppRole.Creator });
        db.Users.Add(creator);
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> AsAdminAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new AdminLoginRequest(SeedData.AdminEmail, SeedData.DemoPassword),
            ApiJson.Options);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<AdminAuthResponse>(ApiJson.Options)
            ?? throw new InvalidOperationException("Admin login returned no body.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    private static async Task<JsonObject> CreateCourseAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/creator/courses", new
        {
            code = $"CRT-{Guid.NewGuid():N}",
            title = "Course Workflow Fixture",
            description = "Creator Course integration fixture.",
            courseLevelId = 1,
            learningPathId = 1,
            category = "Design",
            creditCost = 20,
            learningOutcomes = new[] { "Complete the workflow" },
        });
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    }

    private static object CourseRequest(string code) => new
    {
        code,
        title = "Course Request Fixture",
        description = "Creator Course request fixture.",
        courseLevelId = 1,
        learningPathId = 1,
        category = "Design",
        creditCost = 20,
        learningOutcomes = new[] { "Complete the request" },
    };
}
