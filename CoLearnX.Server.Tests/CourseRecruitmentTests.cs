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

public class CourseRecruitmentTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Published_courses_respect_recruitment_choice_and_notify_matching_trainers(bool open)
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
            openToTrainerApplications = open,
        });
        response.EnsureSuccessStatusCode();
        var course = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(open, course["openToTrainerApplications"]!.GetValue<bool>());
        var id = course["id"]!.GetValue<int>();
        var code = course["code"]!.GetValue<string>();
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

        var available = await trainer.GetFromJsonAsync<JsonArray>("/api/trainer/course-applications/available");
        Assert.Equal(open, available!.Any(item => item!["courseId"]!.GetValue<int>() == id));
        var apply = await trainer.PostAsync($"/api/trainer/course-applications/{id}", null);
        Assert.Equal(open ? HttpStatusCode.OK : HttpStatusCode.Conflict, apply.StatusCode);
        using var check = factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(MaterialVersionStatus.Approved, (await database.CourseMaterialVersions
            .SingleAsync(version => version.LearningMaterialId == materialId)).Status);
        var notices = await database.Notifications.Where(n => n.UserId == trainerId && n.Code == "N-course-seeking-trainer"
            && n.Body.Contains(code)).CountAsync();
        Assert.Equal(open ? 1 : 0, notices);
    }
}
