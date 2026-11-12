using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class CourseRecruitmentTests
{
    // Trainers no longer apply to Courses; a Creator invites them. Publishing only approves the waiting files.
    [Fact]
    public async Task Publishing_a_course_approves_its_files_and_offers_no_trainer_applications()
    {
        using var factory = new CoLearnXApiFactory();
        using var creator = await ApiClient.AsCreatorAsync(factory);
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        int subjectId, interestId, trainerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            subjectId = await db.CourseSubjects.Where(s => s.Name == "Cybersecurity and Infrastructure").Select(s => s.Id).SingleAsync();
            interestId = await db.Interests.Where(i => i.Slug == "cybersecurity").Select(i => i.Id).SingleAsync();
            trainerId = await db.Users.Where(u => u.Email == SeedData.TrainerEmail).Select(u => u.Id).SingleAsync();
        }
        var response = await creator.PostAsJsonAsync("/api/creator/courses", new {
            code = $"REC-{Guid.NewGuid():N}", title = "Security Essentials", courseLevelId = 1,
            courseSubjectId = subjectId, durationHours = 2, creditCost = 40, interestIds = new[] { interestId },
        });
        response.EnsureSuccessStatusCode();
        var course = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        var id = course["id"]!.GetValue<int>();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Recruitment guide"), "title");
        form.Add(new StringContent(id.ToString()), "courseId");
        form.Add(new StringContent("Testing"), "category");
        var file = new ByteArrayContent("%PDF-1.4 recruitment"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "guide.pdf");
        var upload = await creator.PostAsync("/api/materials", form);
        upload.EnsureSuccessStatusCode();
        var materialId = (await upload.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
        (await creator.PostAsync($"/api/creator/courses/{id}/submit", null)).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/courses/{id}/review", new AdminReviewRequest("Approve", null))).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/admin/courses/{id}/review", new AdminReviewRequest("Approve", null))).EnsureSuccessStatusCode();

        var apply = await trainer.PostAsync($"/api/trainer/course-applications/{id}", null);
        Assert.False(apply.IsSuccessStatusCode);
        using var check = factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(MaterialVersionStatus.Approved, (await database.CourseMaterialVersions
            .SingleAsync(version => version.LearningMaterialId == materialId)).Status);
        Assert.False(await database.CourseTrainers.AnyAsync(row => row.CourseId == id && row.TrainerId == trainerId));
    }
}
