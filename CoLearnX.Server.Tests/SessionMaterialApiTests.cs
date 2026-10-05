using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class SessionMaterialApiTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public SessionMaterialApiTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Trainer_uploads_and_downloads_a_material_for_an_owned_session_and_member_reads_it()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var enrollment = await db.Enrollments
            .Include(item => item.CourseSession)
            .FirstAsync(item => item.User.Email == SeedData.MemberEmail && item.Status == EnrollmentStatus.Active);
        var intake = enrollment.CourseSession.CourseIntakeId;
        var session = enrollment.CourseSessionId;
        var title = $"Session handout {Guid.NewGuid():N}";
        var payload = "%PDF-1.7 session handout"u8.ToArray();

        using var form = PdfForm(title, payload);
        using var upload = await trainer.PostAsync($"/api/trainer/intakes/{intake}/sessions/{session}/materials", form);

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var created = await upload.Content.ReadFromJsonAsync<SessionMaterialDto>(ApiJson.Options);
        Assert.NotNull(created);
        Assert.Equal(session, created.CourseSessionId);
        Assert.Equal(title, created.Title);
        Assert.Equal("PDF", created.Format);
        Assert.NotNull(upload.Headers.Location);
        var locationPath = upload.Headers.Location!.IsAbsoluteUri
            ? upload.Headers.Location.AbsolutePath
            : upload.Headers.Location.OriginalString;
        Assert.Equal($"/api/trainer/intakes/{intake}/sessions/{session}/materials/{created.Id}/file",
            locationPath);
        using var locationFile = await trainer.GetAsync(upload.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, locationFile.StatusCode);
        Assert.Equal(payload, await locationFile.Content.ReadAsByteArrayAsync());

        using var trainerList = await trainer.GetAsync($"/api/trainer/intakes/{intake}/sessions/{session}/materials");
        Assert.Equal(HttpStatusCode.OK, trainerList.StatusCode);
        var trainerItems = await trainerList.Content.ReadFromJsonAsync<JsonArray>(ApiJson.Options);
        Assert.Contains(trainerItems!, item => item!["id"]!.GetValue<int>() == created.Id);

        using var trainerFile = await trainer.GetAsync($"/api/trainer/intakes/{intake}/sessions/{session}/materials/{created.Id}/file");
        Assert.Equal(HttpStatusCode.OK, trainerFile.StatusCode);
        Assert.Equal(payload, await trainerFile.Content.ReadAsByteArrayAsync());

        using var memberList = await member.GetAsync($"/api/enrollments/{enrollment.Id}/session-materials");
        Assert.Equal(HttpStatusCode.OK, memberList.StatusCode);
        var memberItems = await memberList.Content.ReadFromJsonAsync<JsonArray>(ApiJson.Options);
        Assert.Contains(memberItems!, item => item!["id"]!.GetValue<int>() == created.Id
            && item["courseSessionId"]!.GetValue<int>() == session);

        using var memberFile = await member.GetAsync($"/api/enrollments/{enrollment.Id}/session-materials/{created.Id}/file");
        Assert.Equal(HttpStatusCode.OK, memberFile.StatusCode);
        Assert.Equal(payload, await memberFile.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Session_material_access_is_scoped_to_owned_session_and_reserved_enrollments_cannot_read()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var enrollment = await db.Enrollments
            .Include(item => item.CourseSession)
            .FirstAsync(item => item.User.Email == SeedData.MemberEmail && item.Status == EnrollmentStatus.Active);
        var otherEnrollment = await db.Enrollments
            .Where(item => item.UserId == enrollment.UserId && item.Id != enrollment.Id
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed))
            .FirstAsync();
        var otherSession = await db.CourseSessions.FirstAsync(item => item.Id != enrollment.CourseSessionId);
        var title = $"Scoped handout {Guid.NewGuid():N}";
        using (var form = PdfForm(title, "scoped"u8.ToArray()))
        using (var upload = await trainer.PostAsync($"/api/trainer/intakes/{enrollment.CourseSession.CourseIntakeId}/sessions/{enrollment.CourseSessionId}/materials", form))
        {
            upload.EnsureSuccessStatusCode();
        }

        var material = await db.SessionMaterials.SingleAsync(item => item.Title == title);
        var otherIntake = await db.CourseSessions.Where(item => item.Id == otherSession.Id)
            .Select(item => item.CourseIntakeId).SingleAsync();
        Assert.Equal(HttpStatusCode.NotFound,
            (await trainer.GetAsync($"/api/trainer/intakes/{otherIntake}/sessions/{otherSession.Id}/materials/{material.Id}/file")).StatusCode);

        using var otherMemberList = await member.GetAsync($"/api/enrollments/{otherEnrollment.Id}/session-materials");
        Assert.Equal(HttpStatusCode.OK, otherMemberList.StatusCode);
        var otherItems = await otherMemberList.Content.ReadFromJsonAsync<JsonArray>(ApiJson.Options);
        Assert.DoesNotContain(otherItems!, item => item!["id"]!.GetValue<int>() == material.Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await member.GetAsync($"/api/enrollments/{otherEnrollment.Id}/session-materials/{material.Id}/file")).StatusCode);

        var previousStatus = enrollment.Status;
        try
        {
            enrollment.Status = EnrollmentStatus.Reserved;
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound,
                (await member.GetAsync($"/api/enrollments/{enrollment.Id}/session-materials")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await member.GetAsync($"/api/enrollments/{enrollment.Id}/session-materials/{material.Id}/file")).StatusCode);
        }
        finally
        {
            enrollment.Status = previousStatus;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Session_material_upload_reuses_material_limits_and_leaves_no_row_for_invalid_file()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var session = await db.CourseSessions.Include(item => item.CourseIntake)
            .FirstAsync(item => item.CourseIntake.TrainerId == db.Users.Where(user => user.Email == SeedData.TrainerEmail).Select(user => user.Id).First());
        var title = $"Bad session upload {Guid.NewGuid():N}";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        var file = new ByteArrayContent("not allowed"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "notes.txt");

        using var response = await trainer.PostAsync($"/api/trainer/intakes/{session.CourseIntakeId}/sessions/{session.Id}/materials", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.Equal("INVALID_SESSION_MATERIAL", error?.Code);
        Assert.Contains("file", error?.FieldErrors?.Keys ?? [], StringComparer.OrdinalIgnoreCase);
        Assert.False(await db.SessionMaterials.AnyAsync(item => item.Title == title));
    }

    [Fact]
    public async Task Session_material_upload_requires_an_authenticated_trainer()
    {
        using var member = await ApiClient.AsMemberAsync(_factory);
        using var anonymous = ApiClient.Anonymous(_factory);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var session = await db.CourseSessions.Include(item => item.CourseIntake).FirstAsync();

        using var memberForm = PdfForm("Member must not upload", "member"u8.ToArray());
        using var memberResponse = await member.PostAsync(
            $"/api/trainer/intakes/{session.CourseIntakeId}/sessions/{session.Id}/materials", memberForm);
        Assert.Equal(HttpStatusCode.Forbidden, memberResponse.StatusCode);

        using var anonymousForm = PdfForm("Anonymous must not upload", "anonymous"u8.ToArray());
        using var anonymousResponse = await anonymous.PostAsync(
            $"/api/trainer/intakes/{session.CourseIntakeId}/sessions/{session.Id}/materials", anonymousForm);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    private static MultipartFormDataContent PdfForm(string title, byte[] payload)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        var file = new ByteArrayContent(payload);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "session-material.pdf");
        return form;
    }
}
