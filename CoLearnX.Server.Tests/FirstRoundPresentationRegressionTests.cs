using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class FirstRoundPresentationRegressionTests
{
    [Fact]
    public async Task Usage_schema_upgrade_is_repeatable_and_preserves_legacy_rows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE MaterialUsageLogs (Id INTEGER PRIMARY KEY, Description TEXT); INSERT INTO MaterialUsageLogs VALUES (9, 'legacy usage');");
        await MaterialUsageSchema.EnsureAsync(db);
        await MaterialUsageSchema.EnsureAsync(db);
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM MaterialUsageLogs WHERE Id = 9 AND CourseIntakeId IS NULL").SingleAsync());
    }

    [Fact]
    public async Task Uploaded_file_is_retained_if_submission_committed_before_the_response_failed()
    {
        using var factory = new CoLearnXApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var course = await db.Courses.FirstAsync(x => x.Creator.Email == SeedData.CreatorEmail);
        var storage = new TrackingStorage();
        var versions = new FailingVersions(async (id, request, ct) =>
        {
            await new MaterialVersionService(db, storage).CreateAsync(id, request, ct);
            throw new InvalidOperationException("The committed response was lost");
        });
        var service = new MaterialService(db, storage, versions);
        using var stream = new MemoryStream([1, 2, 3, 4]);
        var file = new FormFile(stream, 0, 4, "file", "kept.png");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(course.CreatorId, course.Id,
            "Committed material", "Testing", null, file));
        var key = Assert.Single(storage.Keys);
        Assert.True(await db.CourseMaterialVersions.AnyAsync(x => x.FilePath == key));
    }

    [Fact]
    public async Task Enrollment_identifies_the_actual_intake_trainer()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var enrollment = await db.Enrollments.Include(x => x.CourseSession).ThenInclude(x => x.CourseIntake)
            .ThenInclude(x => x.Trainer).FirstAsync(x => x.User.Email == SeedData.MemberEmail);
        var creatorId = await db.Users.Where(x => x.Email == SeedData.CreatorEmail).Select(x => x.Id).SingleAsync();
        var course = await db.Courses.SingleAsync(x => x.Id == enrollment.CourseId);
        course.TrainerId = creatorId;
        await db.SaveChangesAsync();
        var rows = await member.GetFromJsonAsync<JsonElement>("/api/enrollments/my");
        var row = rows.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == enrollment.Id);
        Assert.Equal(enrollment.CourseSession.CourseIntake.Trainer.FullName, row.GetProperty("trainerName").GetString());
    }

    [Fact]
    public async Task Admin_can_find_a_user_without_ledger_entries_and_members_cannot()
    {
        using var factory = new CoLearnXApiFactory();
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        using var member = await ApiClient.AsMemberAsync(factory);
        using var anonymous = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var user = new User { Email = "no-ledger@example.com", FullName = "No Ledger Learner",
            DisplayName = "Learner", CreditBalance = 7, PasswordHash = "unused", IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/users?search=no-ledger")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/admin/users?search=no-ledger")).StatusCode);
        var response = await admin.GetAsync("/api/admin/users?search=no-ledger");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = await response.Content.ReadFromJsonAsync<JsonElement>();
        var found = Assert.Single(rows.EnumerateArray());
        Assert.Equal(user.Id, found.GetProperty("id").GetInt32());
        Assert.Equal(7, found.GetProperty("creditBalance").GetInt32());
        Assert.False(found.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Course_and_admin_review_show_creator_actual_trainers_and_learning_definition()
    {
        using var factory = new CoLearnXApiFactory();
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        using var anonymous = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var course = await db.Courses.Include(x => x.Creator).Include(x => x.LearningPath)
            .FirstAsync(x => x.Code.Contains("2051"));
        var detail = await anonymous.GetFromJsonAsync<JsonElement>($"/api/courses/{course.Id}");
        Assert.True(detail.TryGetProperty("creatorName", out var creator));
        Assert.Equal(course.Creator.FullName, creator.GetString());
        Assert.True(detail.GetProperty("trainerNames").GetArrayLength() > 0);
        Assert.Equal(course.LearningPath?.Name, detail.GetProperty("learningPath").GetString());
        var reviews = await admin.GetFromJsonAsync<JsonElement>("/api/admin/courses?status=Published");
        var review = reviews.EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == course.Id);
        Assert.Equal(course.CreatorId, review.GetProperty("submittedByUserId").GetInt32());
        Assert.True(review.GetProperty("learningOutcomes").GetArrayLength() > 0);
        Assert.Equal(course.LearningPath?.Name, review.GetProperty("learningPath").GetString());
    }

    [Fact]
    public async Task Eligibility_exposes_current_progress_and_ungraded_assessment()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var enrollment = await db.Enrollments.Include(x => x.CourseSession)
            .FirstAsync(x => x.User.Email == SeedData.MemberEmail && x.Course.Code.Contains("2051"));
        var assessment = new Assessment { CourseIntakeId = enrollment.CourseSession.CourseIntakeId,
            Title = "Not yet graded", MaxScore = 100, PassScore = 60 };
        // Load the owner through the actual Intake; no client-supplied identity is used.
        assessment.CreatedByTrainerId = await db.CourseIntakes.Where(x => x.Id == assessment.CourseIntakeId)
            .Select(x => x.TrainerId).SingleAsync();
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync();
        var rows = await member.GetFromJsonAsync<JsonElement>("/api/certificates/eligibility");
        var row = rows.EnumerateArray().Single(x => x.GetProperty("enrollmentId").GetInt32() == enrollment.Id);
        Assert.True(row.TryGetProperty("progressPercent", out var progress));
        Assert.Equal(enrollment.ProgressPercent, progress.GetInt32());
        var item = row.GetProperty("assessments").EnumerateArray().Single(x => x.GetProperty("id").GetInt32() == assessment.Id);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("score").ValueKind);
        Assert.Equal("Not graded", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Reset_link_status_checks_validity_without_consuming_or_disclosing_an_account()
    {
        using var factory = new CoLearnXApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == SeedData.MemberEmail);
        var token = new string('A', 64);
        var entry = new PasswordResetToken { UserId = user.Id,
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), RequestedAt = DateTime.UtcNow };
        db.PasswordResetTokens.Add(entry);
        await db.SaveChangesAsync();
        var response = await client.PostAsJsonAsync("/api/auth/reset-password/status", new { token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("valid").GetBoolean());
        Assert.False(body.TryGetProperty("email", out _));
        await db.Entry(entry).ReloadAsync();
        Assert.Null(entry.UsedAt);
        entry.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var second = await client.PostAsJsonAsync("/api/auth/reset-password/status", new { token });
        Assert.False((await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task Failed_material_submission_removes_the_uploaded_file()
    {
        using var factory = new CoLearnXApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var course = await db.Courses.FirstAsync(x => x.Creator.Email == SeedData.CreatorEmail);
        var storage = new TrackingStorage();
        var service = new MaterialService(db, storage, new FailingVersions());
        using var stream = new MemoryStream([1, 2, 3, 4]);
        var file = new FormFile(stream, 0, 4, "file", "small.png");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(course.CreatorId, course.Id,
            "Small material", "Testing", null, file));
        Assert.Empty(storage.Keys);
    }

    private sealed class TrackingStorage : IFileStorage
    {
        public HashSet<string> Keys { get; } = [];
        public string Provider => "Test";
        public bool CanIssueCloudLinks => false;
        public string? Container => null;
        public Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        { Keys.Add(key); return Task.CompletedTask; }
        public Task DeleteAsync(string key, CancellationToken ct = default)
        { Keys.Remove(key); return Task.CompletedTask; }
        public Task<Stream?> OpenAsync(string key, CancellationToken ct = default) => Task.FromResult<Stream?>(null);
        public Task<Uri?> TryCreateReadUriAsync(string key, TimeSpan lifetime, CancellationToken ct = default) => Task.FromResult<Uri?>(null);
    }

    private sealed class FailingVersions(Func<int, CreateMaterialVersionRequest, CancellationToken, Task<MaterialVersionDto>>? submit = null) : IMaterialVersionService
    {
        public Task<MaterialVersionDto> CreateAsync(int id, CreateMaterialVersionRequest request, CancellationToken ct = default)
            => submit is null ? throw new InvalidOperationException("SqlServerRetryingExecutionStrategy internal diagnostic")
                : submit(id, request, ct);
        public Task<IReadOnlyList<MaterialVersionDto>> ListForAdminAsync(string? status, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<MaterialVersionDto>> ListApprovedAsync(int? courseId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MaterialVersionDto> ReviewAsync(int admin, int version, WorkflowReviewRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MaterialFileResult> OpenFileAsync(int version, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
