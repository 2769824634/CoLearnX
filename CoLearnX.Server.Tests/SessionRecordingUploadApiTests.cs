using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class SessionRecordingUploadApiTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public SessionRecordingUploadApiTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Trainer_uploads_a_video_recording_for_an_owned_session_and_downloads_it()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var session = await db.CourseSessions.Include(item => item.CourseIntake)
            .FirstAsync(item => item.CourseIntake.TrainerId == db.Users
                .Where(user => user.Email == SeedData.TrainerEmail).Select(user => user.Id).First());
        var title = $"Session recording {Guid.NewGuid():N}";
        var payload = "video-payload"u8.ToArray();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        var video = new ByteArrayContent(payload);
        video.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(video, "file", "session-recording.mp4");

        using var upload = await trainer.PostAsync(
            $"/api/trainer/intakes/{session.CourseIntakeId}/sessions/{session.Id}/recordings", form);

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var created = await upload.Content.ReadFromJsonAsync<JsonObject>(ApiJson.Options);
        Assert.NotNull(created);
        Assert.True(created["isUploadedFile"]!.GetValue<bool>());
        Assert.EndsWith(".mp4", created["downloadName"]!.GetValue<string>());
        var id = created["id"]!.GetValue<int>();
        using var listResponse = await trainer.GetAsync(
            $"/api/trainer/intakes/{session.CourseIntakeId}/sessions/{session.Id}/recordings");
        var list = await listResponse.Content.ReadFromJsonAsync<JsonArray>(ApiJson.Options);
        var listed = list!.Single(item => item!["id"]!.GetValue<int>() == id)!;
        Assert.True(listed["isUploadedFile"]!.GetValue<bool>());
        Assert.EndsWith(".mp4", listed["downloadName"]!.GetValue<string>());
        using var download = await trainer.GetAsync(
            $"/api/trainer/intakes/{session.CourseIntakeId}/sessions/{session.Id}/recordings/{id}/file");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("video/mp4", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(payload, await download.Content.ReadAsByteArrayAsync());
        var otherSessionId = await db.CourseSessions
            .Where(item => item.CourseIntakeId == session.CourseIntakeId && item.Id != session.Id)
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync();
        if (otherSessionId is not null)
        {
            using var crossSession = await trainer.GetAsync(
                $"/api/trainer/intakes/{session.CourseIntakeId}/sessions/{otherSessionId}/recordings/{id}/file");
            Assert.Equal(HttpStatusCode.NotFound, crossSession.StatusCode);
        }
    }

    [Fact]
    public async Task Member_downloads_an_uploaded_recording_through_the_enrolled_learning_hub()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var enrollment = await db.Enrollments.Include(item => item.CourseSession)
            .FirstAsync(item => item.User.Email == SeedData.MemberEmail
                && item.Status == EnrollmentStatus.Active
                && item.CourseSession.CourseIntake.Trainer.Email == SeedData.TrainerEmail);
        var title = $"Member recording {Guid.NewGuid():N}";
        var payload = "member-video"u8.ToArray();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        var video = new ByteArrayContent(payload);
        video.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(video, "file", "member-recording.mp4");
        using var upload = await trainer.PostAsync(
            $"/api/trainer/intakes/{enrollment.CourseSession.CourseIntakeId}/sessions/{enrollment.CourseSessionId}/recordings", form);
        upload.EnsureSuccessStatusCode();
        var created = await upload.Content.ReadFromJsonAsync<JsonObject>(ApiJson.Options);
        var recordingId = created!["id"]!.GetValue<int>();

        using var listResponse = await member.GetAsync($"/api/enrollments/{enrollment.Id}/recordings");
        listResponse.EnsureSuccessStatusCode();
        var recordings = await listResponse.Content.ReadFromJsonAsync<JsonArray>(ApiJson.Options);
        var recording = recordings!.Single(item => item!["id"]!.GetValue<int>() == recordingId)!;
        var downloadPath = recording["recordingUrl"]!.GetValue<string>();
        Assert.True(recording["isUploadedFile"]!.GetValue<bool>());
        Assert.EndsWith(".mp4", recording["downloadName"]!.GetValue<string>());
        Assert.Equal($"/api/enrollments/{enrollment.Id}/recordings/{recordingId}/file", downloadPath);

        using var download = await member.GetAsync(downloadPath);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(payload, await download.Content.ReadAsByteArrayAsync());
        var otherEnrollmentId = await db.Enrollments
            .Where(item => item.User.Email == SeedData.MemberEmail && item.Id != enrollment.Id
                && item.CourseSessionId != enrollment.CourseSessionId
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed))
            .Select(item => (int?)item.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(otherEnrollmentId);
        using var crossEnrollment = await member.GetAsync(
            $"/api/enrollments/{otherEnrollmentId}/recordings/{recordingId}/file");
        Assert.Equal(HttpStatusCode.NotFound, crossEnrollment.StatusCode);
    }

    [Fact]
    public async Task Recording_upload_rejects_a_non_video_content_type_even_with_a_video_extension()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var session = await db.CourseSessions.Include(item => item.CourseIntake)
            .FirstAsync(item => item.CourseIntake.TrainerId == db.Users
                .Where(user => user.Email == SeedData.TrainerEmail).Select(user => user.Id).First());
        var title = $"Invalid recording {Guid.NewGuid():N}";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        var fakeVideo = new ByteArrayContent("plain text"u8.ToArray());
        fakeVideo.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(fakeVideo, "file", "not-really-video.mp4");

        using var response = await trainer.PostAsync(
            $"/api/trainer/intakes/{session.CourseIntakeId}/sessions/{session.Id}/recordings", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(ApiJson.Options);
        Assert.Equal("INVALID_RECORDING", body!["code"]!.GetValue<string>());
        Assert.False(await db.SessionRecordings.AnyAsync(item => item.Title == title));
    }
}
