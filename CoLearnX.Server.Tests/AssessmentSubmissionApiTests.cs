using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class AssessmentSubmissionApiTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public AssessmentSubmissionApiTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Member_submits_several_documents_before_the_deadline_and_the_trainer_downloads_them()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        var enrollment = await ActiveEnrollmentAsync();
        var assessment = await CreateAssessmentAsync(trainer, enrollment.IntakeId, DateTime.UtcNow.AddDays(2));
        var essay = "%PDF-1.7 essay"u8.ToArray();
        var slides = "pptx slides"u8.ToArray();

        using var form = Files(("essay.pdf", "application/pdf", essay), ("slides.pptx", "application/octet-stream", slides));
        using var submit = await member.PostAsync(SubmissionPath(enrollment.Id, assessment.Id), form);

        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var submitted = await submit.Content.ReadFromJsonAsync<MemberAssessmentDto>(ApiJson.Options);
        Assert.NotNull(submitted?.Files);
        Assert.Equal(new[] { "essay.pdf", "slides.pptx" }, submitted.Files!.Select(item => item.FileName));
        Assert.Equal(new[] { "PDF", "PPTX" }, submitted.Files!.Select(item => item.Format));
        Assert.NotNull(submitted.SubmittedAt);
        Assert.True(submitted.CanSubmit);

        var listed = await member.GetFromJsonAsync<JsonArray>($"/api/enrollments/{enrollment.Id}/assessments", ApiJson.Options);
        var item = Assert.Single(listed!, row => row!["id"]!.GetValue<int>() == assessment.Id)!;
        Assert.Equal(2, item["files"]!.AsArray().Count);
        Assert.True(item["canSubmit"]!.GetValue<bool>());

        var essayId = submitted.Files![0].Id;
        using var ownFile = await member.GetAsync($"{SubmissionPath(enrollment.Id, assessment.Id)}/files/{essayId}");
        Assert.Equal(HttpStatusCode.OK, ownFile.StatusCode);
        Assert.Equal(essay, await ownFile.Content.ReadAsByteArrayAsync());

        var trainerList = await trainer.GetFromJsonAsync<List<AssessmentDto>>(
            $"/api/trainer/intakes/{enrollment.IntakeId}/assessments", ApiJson.Options);
        var trainerAssessment = Assert.Single(trainerList!, row => row.Id == assessment.Id);
        var learner = Assert.Single(trainerAssessment.Submissions!);
        Assert.Equal(enrollment.Id, learner.EnrollmentId);
        Assert.Equal(2, learner.Files.Count);

        using var trainerFile = await trainer.GetAsync($"/api/trainer/assessments/{assessment.Id}/submissions/files/{essayId}");
        Assert.Equal(HttpStatusCode.OK, trainerFile.StatusCode);
        Assert.Equal(essay, await trainerFile.Content.ReadAsByteArrayAsync());
        Assert.Equal("essay.pdf", trainerFile.Content.Headers.ContentDisposition?.FileNameStar
            ?? trainerFile.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    [Fact]
    public async Task Submitting_again_before_the_deadline_replaces_the_previous_documents()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        var enrollment = await ActiveEnrollmentAsync();
        var assessment = await CreateAssessmentAsync(trainer, enrollment.IntakeId, DateTime.UtcNow.AddDays(1));

        using (var first = Files(("draft.pdf", "application/pdf", "draft"u8.ToArray()), ("notes.docx", "application/octet-stream", "notes"u8.ToArray())))
            (await member.PostAsync(SubmissionPath(enrollment.Id, assessment.Id), first)).EnsureSuccessStatusCode();
        using var second = Files(("final.pdf", "application/pdf", "final"u8.ToArray()));
        using var response = await member.PostAsync(SubmissionPath(enrollment.Id, assessment.Id), second);
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var names = await db.AssessmentSubmissionFiles
            .Where(item => item.AssessmentId == assessment.Id && item.EnrollmentId == enrollment.Id)
            .Select(item => item.OriginalFileName).ToListAsync();
        Assert.Equal(new[] { "final.pdf" }, names);
    }

    [Fact]
    public async Task Submission_after_the_deadline_is_rejected_and_nothing_is_saved()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        var enrollment = await ActiveEnrollmentAsync();
        var assessment = await CreateAssessmentAsync(trainer, enrollment.IntakeId, DateTime.UtcNow.AddMinutes(-1));

        using var form = Files(("late.pdf", "application/pdf", "late"u8.ToArray()));
        using var response = await member.PostAsync(SubmissionPath(enrollment.Id, assessment.Id), form);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SUBMISSION_CLOSED", (await ApiClient.ReadErrorAsync(response))?.Code);
        var listed = await member.GetFromJsonAsync<JsonArray>($"/api/enrollments/{enrollment.Id}/assessments", ApiJson.Options);
        var item = Assert.Single(listed!, row => row!["id"]!.GetValue<int>() == assessment.Id)!;
        Assert.False(item["canSubmit"]!.GetValue<bool>());
        Assert.Empty(item["files"]!.AsArray());
        Assert.False(await ReadAsync(db => db.AssessmentSubmissionFiles.AnyAsync(row => row.AssessmentId == assessment.Id)));
    }

    [Fact]
    public async Task Graded_assessment_no_longer_accepts_documents()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        var enrollment = await ActiveEnrollmentAsync();
        var assessment = await CreateAssessmentAsync(trainer, enrollment.IntakeId, DateTime.UtcNow.AddDays(3));
        (await trainer.PutAsJsonAsync($"/api/trainer/assessments/{assessment.Id}/grades/{enrollment.Id}",
            new GradeAssessmentRequest(80, null))).EnsureSuccessStatusCode();

        using var form = Files(("after-grade.pdf", "application/pdf", "x"u8.ToArray()));
        using var response = await member.PostAsync(SubmissionPath(enrollment.Id, assessment.Id), form);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SUBMISSION_CLOSED", (await ApiClient.ReadErrorAsync(response))?.Code);
    }

    [Fact]
    public async Task Invalid_type_too_many_files_or_no_file_are_rejected()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        var enrollment = await ActiveEnrollmentAsync();
        var assessment = await CreateAssessmentAsync(trainer, enrollment.IntakeId, DateTime.UtcNow.AddDays(1));
        var path = SubmissionPath(enrollment.Id, assessment.Id);

        using var text = Files(("notes.txt", "text/plain", "plain"u8.ToArray()));
        using var textResponse = await member.PostAsync(path, text);
        Assert.Equal(HttpStatusCode.BadRequest, textResponse.StatusCode);
        Assert.Equal("INVALID_SUBMISSION", (await ApiClient.ReadErrorAsync(textResponse))?.Code);

        using var six = Files(Enumerable.Range(1, 6).Select(index => ($"part-{index}.pdf", "application/pdf", "p"u8.ToArray())).ToArray());
        using var sixResponse = await member.PostAsync(path, six);
        Assert.Equal(HttpStatusCode.BadRequest, sixResponse.StatusCode);

        using var empty = new MultipartFormDataContent { { new StringContent("none"), "note" } };
        using var emptyResponse = await member.PostAsync(path, empty);
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);

        Assert.False(await ReadAsync(db => db.AssessmentSubmissionFiles.AnyAsync(row => row.AssessmentId == assessment.Id)));
    }

    [Fact]
    public async Task Member_cannot_submit_to_another_intake_and_other_trainers_cannot_download()
    {
        using var trainer = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        using var member = await ApiClient.AsMemberAsync(_factory);
        var enrollment = await ActiveEnrollmentAsync();
        var assessment = await CreateAssessmentAsync(trainer, enrollment.IntakeId, DateTime.UtcNow.AddDays(1));
        var otherEnrollmentId = await ReadAsync(db => db.Enrollments
            .Where(item => item.User.Email == SeedData.MemberEmail && item.Id != enrollment.Id
                && item.CourseSession.CourseIntakeId != enrollment.IntakeId
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed))
            .Select(item => (int?)item.Id).FirstOrDefaultAsync());

        if (otherEnrollmentId is int otherId)
        {
            using var wrongIntake = Files(("wrong.pdf", "application/pdf", "w"u8.ToArray()));
            using var wrongResponse = await member.PostAsync(SubmissionPath(otherId, assessment.Id), wrongIntake);
            Assert.Equal(HttpStatusCode.NotFound, wrongResponse.StatusCode);
        }

        using var form = Files(("mine.pdf", "application/pdf", "m"u8.ToArray()));
        var submitted = await (await member.PostAsync(SubmissionPath(enrollment.Id, assessment.Id), form))
            .Content.ReadFromJsonAsync<MemberAssessmentDto>(ApiJson.Options);
        var fileId = submitted!.Files![0].Id;
        using var otherTrainer = await OtherTrainerClientAsync();
        if (otherTrainer is not null)
        {
            using var denied = await otherTrainer.GetAsync($"/api/trainer/assessments/{assessment.Id}/submissions/files/{fileId}");
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }

        using var anonymous = ApiClient.Anonymous(_factory);
        using var anonymousForm = Files(("anon.pdf", "application/pdf", "a"u8.ToArray()));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync(SubmissionPath(enrollment.Id, assessment.Id), anonymousForm)).StatusCode);
    }

    [Fact]
    public async Task Existing_sqlite_database_gains_the_submission_table_and_repeated_upgrade_is_safe()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE Assessments (Id INTEGER PRIMARY KEY); INSERT INTO Assessments (Id) VALUES (1);
            CREATE TABLE Enrollments (Id INTEGER PRIMARY KEY); INSERT INTO Enrollments (Id) VALUES (1);
            """);

        await AssessmentSubmissionSchema.EnsureAsync(db);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO AssessmentSubmissionFiles (AssessmentId, EnrollmentId, OriginalFileName, FilePath, Format, SizeBytes, SubmittedAt) VALUES (1, 1, 'essay.pdf', 'assessment-submissions/1/1/a.pdf', 'PDF', 10, '2026-11-12T00:00:00Z');");
        await AssessmentSubmissionSchema.EnsureAsync(db);

        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM AssessmentSubmissionFiles").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_index_list('AssessmentSubmissionFiles') WHERE name = 'IX_AssessmentSubmissionFiles_AssessmentId_EnrollmentId'").SingleAsync());
    }

    private static string SubmissionPath(int enrollmentId, int assessmentId)
        => $"/api/enrollments/{enrollmentId}/assessments/{assessmentId}/submission";

    private static MultipartFormDataContent Files(params (string Name, string ContentType, byte[] Bytes)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, contentType, bytes) in files)
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(content, "files", name);
        }
        return form;
    }

    private async Task<AssessmentDto> CreateAssessmentAsync(HttpClient trainer, int intakeId, DateTime dueAt)
    {
        using var response = await trainer.PostAsJsonAsync($"/api/trainer/intakes/{intakeId}/assessments",
            new CreateAssessmentRequest($"Assignment {Guid.NewGuid():N}", 100, 60, dueAt));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssessmentDto>(ApiJson.Options))!;
    }

    private async Task<(int Id, int IntakeId)> ActiveEnrollmentAsync()
        => await ReadAsync(async db =>
        {
            var row = await db.Enrollments
                .Where(item => item.User.Email == SeedData.MemberEmail && item.Status == EnrollmentStatus.Active
                    && item.CourseSession.CourseIntake.Trainer.Email == SeedData.TrainerEmail)
                .Select(item => new { item.Id, item.CourseSession.CourseIntakeId })
                .FirstAsync();
            return (row.Id, row.CourseIntakeId);
        });

    private async Task<HttpClient?> OtherTrainerClientAsync()
    {
        var email = await ReadAsync(db => db.Users
            .Where(user => user.Email != SeedData.TrainerEmail && user.IsActive
                && user.Roles.Any(role => role.Role == AppRole.Trainer))
            .Select(user => user.Email).FirstOrDefaultAsync());
        if (email is null) return null;
        try { return await ApiClient.AsRoleAsync(_factory, email, "Trainer"); }
        catch (HttpRequestException) { return null; }
    }

    private async Task<T> ReadAsync<T>(Func<CoLearnXDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>());
    }
}
