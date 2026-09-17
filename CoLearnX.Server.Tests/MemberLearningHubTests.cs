using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class MemberLearningHubTests
{
    [Fact]
    public async Task Enrolled_member_can_read_attached_material_and_session_recording_only()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var enrollment = await db.Enrollments.Include(item => item.CourseSession).ThenInclude(item => item.CourseIntake)
            .FirstAsync(item => item.User.Email == SeedData.MemberEmail);
        var creator = await db.Users.FirstAsync(item => item.Email == SeedData.CreatorEmail);
        var material = new LearningMaterial { CreatorId = creator.Id, Title = "Hub guide", FilePath = "hub/guide.pdf" };
        db.LearningMaterials.Add(material);
        await db.SaveChangesAsync();
        var version = new CourseMaterialVersion { LearningMaterialId = material.Id, FilePath = "hub/guide.pdf", Status = MaterialVersionStatus.Approved };
        db.CourseMaterialVersions.Add(version);
        await db.SaveChangesAsync();
        db.CourseIntakeMaterials.Add(new CourseIntakeMaterial
        {
            CourseIntakeId = enrollment.CourseSession.CourseIntakeId,
            CourseMaterialVersionId = version.Id,
            AttachedByTrainerId = enrollment.CourseSession.CourseIntake.TrainerId,
        });
        db.SessionRecordings.Add(new SessionRecording
        {
            CourseSessionId = enrollment.CourseSessionId,
            AddedByTrainerId = enrollment.CourseSession.CourseIntake.TrainerId,
            Title = "Replay", RecordingUrl = "https://example.com/replay",
        });
        await db.SaveChangesAsync();
        await storage.SaveAsync("hub/guide.pdf", new MemoryStream("guide"u8.ToArray()), "application/pdf");

        var materials = await member.GetFromJsonAsync<JsonArray>($"/api/enrollments/{enrollment.Id}/materials", ApiJson.Options);
        var recordings = await member.GetFromJsonAsync<JsonArray>($"/api/enrollments/{enrollment.Id}/recordings", ApiJson.Options);
        var file = await member.GetAsync($"/api/enrollments/{enrollment.Id}/materials/{version.Id}/file");

        Assert.Contains(materials!, item => item!["id"]!.GetValue<int>() == version.Id && item["title"]!.GetValue<string>() == "Hub guide");
        Assert.Contains(recordings!, item => item!["title"]!.GetValue<string>() == "Replay");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("guide", await file.Content.ReadAsStringAsync());

        using var stranger = factory.CreateClient();
        var email = $"hub-{Guid.NewGuid():N}@example.com";
        (await stranger.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, SeedData.DemoPassword, "Other Member", "Other"), ApiJson.Options)).EnsureSuccessStatusCode();
        using var otherMember = await ApiClient.AsMemberAsync(factory, email);
        Assert.Equal(HttpStatusCode.NotFound, (await otherMember.GetAsync($"/api/enrollments/{enrollment.Id}/materials")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherMember.GetAsync($"/api/enrollments/{enrollment.Id}/recordings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherMember.GetAsync($"/api/enrollments/{enrollment.Id}/materials/{version.Id}/file")).StatusCode);
    }
}
