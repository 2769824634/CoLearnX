using System.Collections.Concurrent;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class AssessmentSubmissionConcurrencyTests(CoLearnXApiFactory factory) : IClassFixture<CoLearnXApiFactory>
{
    [Fact]
    public async Task Simultaneous_submissions_leave_one_complete_replacement()
    {
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var setupScope = factory.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var enrollment = await setupDb.Enrollments.AsNoTracking()
            .Where(item => item.User.Email == SeedData.MemberEmail && item.Status == EnrollmentStatus.Active
                && item.CourseSession.CourseIntake.Trainer.Email == SeedData.TrainerEmail)
            .Select(item => new { item.Id, item.UserId, item.CourseSession.CourseIntakeId })
            .FirstAsync();
        using var response = await trainer.PostAsJsonAsync($"/api/trainer/intakes/{enrollment.CourseIntakeId}/assessments",
            new CreateAssessmentRequest("Concurrent submissions", 100, 60, DateTime.UtcNow.AddDays(1)));
        response.EnsureSuccessStatusCode();
        var assessment = (await response.Content.ReadFromJsonAsync<AssessmentDto>(ApiJson.Options))!;

        var storage = new ConcurrentStorage();
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = new AssessmentSubmissionService(firstScope.ServiceProvider.GetRequiredService<CoLearnXDbContext>(), storage);
        var second = new AssessmentSubmissionService(secondScope.ServiceProvider.GetRequiredService<CoLearnXDbContext>(), storage);
        using var firstBytes = new MemoryStream("first"u8.ToArray());
        using var secondBytes = new MemoryStream("second"u8.ToArray());
        IFormFile firstFile = new FormFile(firstBytes, 0, firstBytes.Length, "files", "first.pdf");
        IFormFile secondFile = new FormFile(secondBytes, 0, secondBytes.Length, "files", "second.pdf");

        await Task.WhenAll(
            first.SubmitAsync(enrollment.UserId, enrollment.Id, assessment.Id, [firstFile]),
            second.SubmitAsync(enrollment.UserId, enrollment.Id, assessment.Id, [secondFile]));

        var stored = await setupDb.AssessmentSubmissionFiles.AsNoTracking()
            .Where(item => item.AssessmentId == assessment.Id && item.EnrollmentId == enrollment.Id)
            .ToListAsync();
        Assert.Single(stored);
        Assert.Contains(stored[0].OriginalFileName, new[] { "first.pdf", "second.pdf" });
        Assert.Single(storage.Keys);
        Assert.Contains(stored[0].FilePath, storage.Keys);
    }

    [Fact]
    public async Task Grading_during_upload_closes_submission_and_cleans_the_new_file()
    {
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var setupScope = factory.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var enrollment = await setupDb.Enrollments.AsNoTracking()
            .Where(item => item.User.Email == SeedData.MemberEmail && item.Status == EnrollmentStatus.Active
                && item.CourseSession.CourseIntake.Trainer.Email == SeedData.TrainerEmail)
            .Select(item => new { item.Id, item.UserId, item.CourseSession.CourseIntakeId })
            .FirstAsync();
        using var response = await trainer.PostAsJsonAsync($"/api/trainer/intakes/{enrollment.CourseIntakeId}/assessments",
            new CreateAssessmentRequest("Grade during upload", 100, 60, DateTime.UtcNow.AddDays(1)));
        response.EnsureSuccessStatusCode();
        var assessment = (await response.Content.ReadFromJsonAsync<AssessmentDto>(ApiJson.Options))!;

        var storage = new PausedStorage();
        using var scope = factory.Services.CreateScope();
        var service = new AssessmentSubmissionService(scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>(), storage);
        using var bytes = new MemoryStream("draft"u8.ToArray());
        IFormFile file = new FormFile(bytes, 0, bytes.Length, "files", "draft.pdf");
        var pending = service.SubmitAsync(enrollment.UserId, enrollment.Id, assessment.Id, [file]);
        await storage.Started.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            using var grade = await trainer.PutAsJsonAsync($"/api/trainer/assessments/{assessment.Id}/grades/{enrollment.Id}",
                new GradeAssessmentRequest(80, null));
            grade.EnsureSuccessStatusCode();
        }
        finally { storage.Release(); }

        var error = await Assert.ThrowsAsync<LaterPhaseException>(() => pending);
        Assert.Equal("SUBMISSION_CLOSED", error.Code);
        Assert.False(await setupDb.AssessmentSubmissionFiles.AnyAsync(item => item.AssessmentId == assessment.Id));
        Assert.Empty(storage.Keys);
    }

    private sealed class PausedStorage : IFileStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Provider => "Test";
        public bool CanIssueCloudLinks => false;
        public string? Container => null;
        public IReadOnlyCollection<string> Keys => _files.Keys.ToArray();
        public Task Started => _started.Task;
        public void Release() => _resume.TrySetResult(true);

        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, ct);
            _started.TrySetResult(true);
            await _resume.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            _files[key] = copy.ToArray();
        }

        public Task<Stream?> OpenAsync(string key, CancellationToken ct = default)
            => Task.FromResult<Stream?>(_files.TryGetValue(key, out var data) ? new MemoryStream(data) : null);

        public Task<Uri?> TryCreateReadUriAsync(string key, TimeSpan lifetime, CancellationToken ct = default)
            => Task.FromResult<Uri?>(null);

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            _files.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }

    private sealed class ConcurrentStorage : IFileStorage
    {
        private readonly ConcurrentDictionary<string, byte[]> _files = new();
        private readonly TaskCompletionSource<bool> _bothSaving = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public string Provider => "Test";
        public bool CanIssueCloudLinks => false;
        public string? Container => null;
        public IReadOnlyCollection<string> Keys => _files.Keys.ToArray();

        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, ct);
            if (Interlocked.Increment(ref _arrived) == 2) _bothSaving.TrySetResult(true);
            await _bothSaving.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
            _files[key] = copy.ToArray();
        }

        public Task<Stream?> OpenAsync(string key, CancellationToken ct = default)
            => Task.FromResult<Stream?>(_files.TryGetValue(key, out var data) ? new MemoryStream(data) : null);

        public Task<Uri?> TryCreateReadUriAsync(string key, TimeSpan lifetime, CancellationToken ct = default)
            => Task.FromResult<Uri?>(null);

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            _files.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }
}
