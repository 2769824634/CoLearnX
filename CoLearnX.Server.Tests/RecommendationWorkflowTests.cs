using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class RecommendationWorkflowTests
{
    [Fact]
    public async Task MissingNextLevelFallsBackToMasteryAtCurrentLevel()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int masteryId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var anchor = await db.Courses.SingleAsync(c => c.Code == "INFT 2002");
            var leaf = await db.Interests.SingleAsync(i => i.Slug == "frontend-development");
            var course = new Course { Code = $"REC-{Guid.NewGuid():N}", Title = "Frontend Mastery",
                TrainerId = anchor.TrainerId, CreatorId = anchor.CreatorId,
                CreditCost = 20, CourseLevelId = anchor.CourseLevelId, LearningPathId = anchor.LearningPathId,
                Level = anchor.Level, Category = anchor.Category, Status = CourseStatus.Published };
            course.Interests.Add(new CourseInterest { InterestId = leaf.Id });
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            masteryId = course.Id;
            await OpenIntakeAsync(db, course);
        }

        var response = await member.GetFromJsonAsync<JsonObject>("/api/recommendations");
        Assert.Equal("mastery", response!["mode"]!.GetValue<string>());
        Assert.Contains(response["items"]!.AsArray(), item => item!["courseId"]!.GetValue<int>() == masteryId);
    }

    [Fact]
    public async Task CompletedCourseUsesItsLeafTagsAndAssessmentGateForProgression()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int nextCourseId;
        int anchorIntakeId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var anchor = await db.Courses.SingleAsync(c => c.Code == "INFT 2002");
            var nextLevel = await db.CourseLevels.SingleAsync(l => l.Name == "Intermediate");
            var tag = await db.Interests.SingleAsync(i => i.Slug == "frontend-development");
            var course = new Course { Code = $"REC-{Guid.NewGuid():N}", Title = "Intermediate Frontend",
                TrainerId = anchor.TrainerId, CreatorId = anchor.CreatorId,
                CreditCost = 20, CourseLevelId = nextLevel.Id, LearningPathId = anchor.LearningPathId,
                Level = nextLevel.Name, Category = "Programming", Status = CourseStatus.Published };
            course.Interests.Add(new CourseInterest { InterestId = tag.Id });
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            nextCourseId = course.Id;
            anchorIntakeId = await db.Enrollments.Where(e => e.CourseId == anchor.Id && e.Status == EnrollmentStatus.Completed
                    && e.User.Email == SeedData.MemberEmail)
                .Select(e => e.CourseSession.CourseIntakeId).SingleAsync();
            await OpenIntakeAsync(db, course);
        }

        var next = await member.GetFromJsonAsync<JsonObject>("/api/recommendations");
        Assert.Equal("nextLevel", next!["mode"]!.GetValue<string>());
        Assert.Contains(next["items"]!.AsArray(), item => item!["courseId"]!.GetValue<int>() == nextCourseId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            db.Assessments.Add(new Assessment { CourseIntakeId = anchorIntakeId,
                CreatedByTrainerId = await db.Users.Where(u => u.Email == SeedData.TrainerEmail).Select(u => u.Id).SingleAsync(),
                Title = "Progression gate", MaxScore = 100, PassScore = 60 });
            await db.SaveChangesAsync();
        }
        var same = await member.GetFromJsonAsync<JsonObject>("/api/recommendations");
        Assert.Equal("sameLevel", same!["mode"]!.GetValue<string>());
        Assert.DoesNotContain(same["items"]!.AsArray(), item => item!["courseId"]!.GetValue<int>() == nextCourseId);
    }

    [Fact]
    public async Task SkipOnboardingUsesAllOpenBeginnerCourses()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int beginnerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var memberId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
            var enrollments = await db.Enrollments.Where(e => e.UserId == memberId).ToListAsync();
            foreach (var enrollment in enrollments) enrollment.Status = EnrollmentStatus.Cancelled;
            var beginner = await db.Courses.SingleAsync(c => c.Code == "INFT 2051");
            beginnerId = beginner.Id;
            await OpenIntakeAsync(db, beginner);
            await db.SaveChangesAsync();
        }
        var skipped = await member.PutAsJsonAsync("/api/users/me/interests", new { skip = true });
        Assert.Equal(HttpStatusCode.OK, skipped.StatusCode);
        var recommendations = await member.GetFromJsonAsync<JsonObject>("/api/recommendations");
        Assert.Equal("coldStart", recommendations!["mode"]!.GetValue<string>());
        Assert.Contains(recommendations["items"]!.AsArray(), item => item!["courseId"]!.GetValue<int>() == beginnerId);
    }

    private static async Task OpenIntakeAsync(CoLearnXDbContext db, Course course)
    {
        var start = DateTime.UtcNow.AddDays(15);
        var intake = new CourseIntake { CourseId = course.Id, TrainerId = course.TrainerId,
            RegistrationOpensAt = DateTime.UtcNow.AddDays(-1), RegistrationClosesAt = DateTime.UtcNow.AddDays(10),
            StartsAt = start, EndsAt = start.AddDays(1), Status = CourseIntakeStatus.Published };
        intake.Sessions.Add(new CourseSession { Label = "Online", StartsAt = start,
            EndsAt = start.AddHours(2), PhysicalCapacity = 0, MeetingLink = "https://example.com/meeting" });
        db.CourseIntakes.Add(intake);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TrainerCanCompleteActiveEnrollmentOnlyAfterAllAssessmentsPass()
    {
        using var factory = new CoLearnXApiFactory();
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        int enrollmentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            enrollmentId = await db.Enrollments.Where(e => e.Status == EnrollmentStatus.Active && e.Course.Code == "INFT 3030")
                .Select(e => e.Id).SingleAsync();
        }
        var response = await trainer.PostAsync($"/api/trainer/enrollments/{enrollmentId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<CoLearnXDbContext>().Enrollments.FindAsync(enrollmentId);
        Assert.Equal(EnrollmentStatus.Completed, saved?.Status);
        Assert.Equal(100, saved?.ProgressPercent);
    }

    [Fact]
    public async Task InterestsAreSeededAsTwoLevelTree_AndOnboardingRejectsParent()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        var tree = await member.GetFromJsonAsync<JsonArray>("/api/interests");
        Assert.Equal(15, tree?.Count);
        var it = tree!.Single(item => item!["slug"]!.GetValue<string>() == "it-software")!;
        var cyber = it["children"]!.AsArray().Single(item => item!["slug"]!.GetValue<string>() == "cybersecurity")!;

        var invalid = await member.PutAsJsonAsync("/api/users/me/interests", new { interestIds = new[] { it["id"]!.GetValue<int>() }, learningGoals = "professional", skip = false });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("INTEREST_NOT_LEAF", (await ApiClient.ReadErrorAsync(invalid))?.Code);

        var saved = await member.PutAsJsonAsync("/api/users/me/interests", new { interestIds = new[] { cyber["id"]!.GetValue<int>() }, learningGoals = "professional", skip = false });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var state = await member.GetFromJsonAsync<JsonObject>("/api/users/me/interests");
        Assert.Equal("professional", state!["learningGoals"]!.GetValue<string>());
        Assert.NotNull(state["onboardingCompletedAt"]);
        Assert.Single(state["interestIds"]!.AsArray());
    }

    [Fact]
    public async Task CourseTagsAndCompletedRatingDriveRecommendations()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var creator = await ApiClient.AsCreatorAsync(factory);
        var tree = await member.GetFromJsonAsync<JsonArray>("/api/interests");
        var ux = tree!.Single(item => item!["slug"]!.GetValue<string>() == "design")!["children"]!.AsArray()
            .Single(item => item!["slug"]!.GetValue<string>() == "user-experience-design")!["id"]!.GetValue<int>();
        var created = await creator.PostAsJsonAsync("/api/creator/courses", new {
            code = $"REC-{Guid.NewGuid():N}", title = "UX Progression", courseLevelId = 2,
            learningPathId = 1, category = "Design", creditCost = 10,
            interestIds = new[] { ux }, learningOutcomes = new[] { "Advance" }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var course = await created.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(ux, course!["interestIds"]![0]!.GetValue<int>());

        int completedId;
        int activeId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            completedId = await db.Enrollments.Where(e => e.Status == EnrollmentStatus.Completed).Select(e => e.Id).FirstAsync();
            activeId = await db.Enrollments.Where(e => e.Status == EnrollmentStatus.Active).Select(e => e.Id).FirstAsync();
        }
        var early = await member.PostAsJsonAsync($"/api/enrollments/{activeId}/rating", new { stars = 5, comment = "Too early" });
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Equal("RATING_NOT_COMPLETED", (await ApiClient.ReadErrorAsync(early))?.Code);

        var rated = await member.PostAsJsonAsync($"/api/enrollments/{completedId}/rating", new { stars = 5, comment = "Useful" });
        Assert.Equal(HttpStatusCode.OK, rated.StatusCode);
        var recommendations = await member.GetFromJsonAsync<JsonObject>("/api/recommendations");
        Assert.NotNull(recommendations!["mode"]);
        Assert.NotNull(recommendations["items"]);
    }
}
