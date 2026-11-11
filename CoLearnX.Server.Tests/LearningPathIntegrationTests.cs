using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class LearningPathIntegrationTests
{
    [Fact]
    public async Task MemberCanSeeOneThreeStagePathPerSubject()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var response = await member.GetAsync("/api/learning-paths/my");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paths = await response.Content.ReadFromJsonAsync<JsonArray>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var subjectIds = await db.CourseSubjects.Select(subject => subject.Id).ToListAsync();
        Assert.Equal(subjectIds.Count, paths!.Count);
        Assert.Equal(subjectIds.Order(), paths.Select(path => path!["subjectId"]!.GetValue<int>()).Order());
        foreach (var path in paths)
        {
            var stages = path!["stages"]!.AsArray();
            Assert.Equal(new[] { "Foundation", "Intermediate", "Advanced" },
                stages.Select(stage => stage!["name"]!.GetValue<string>()));
            Assert.All(stages, stage => Assert.Equal(1, stage!["requiredCourseCount"]!.GetValue<int>()));
        }
    }

    [Fact]
    public async Task CreatorCourseGetsPathFromItsSubjectWithoutChoosingAPath()
    {
        using var factory = new CoLearnXApiFactory();
        using var creator = await ApiClient.AsCreatorAsync(factory);
        int subjectId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            subjectId = await db.UserSpecialties.Where(s => s.User.Email == SeedData.CreatorEmail && s.CourseSubject.Name == "Software Development")
                .Select(s => s.CourseSubjectId).SingleAsync();
        }
        var response = await creator.PostAsJsonAsync("/api/creator/courses", new {
            code = $"PATH-{Guid.NewGuid():N}", title = "Path course", courseLevelId = 1,
            courseSubjectId = subjectId, durationHours = 2, creditCost = 80,
            learningOutcomes = Array.Empty<string>()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("Software Development", course!["learningPathName"]!.GetValue<string>());
    }

    [Fact]
    public async Task HistoricalCourseCertificatesAwardStagesAndGraduationOnlyOnce()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int subjectId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            subjectId = await db.CourseSubjects.Where(s => s.Name == "Software Development").Select(s => s.Id).SingleAsync();
            var userId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
            var example = await db.Courses.FirstAsync();
            var templates = await db.CertificateTemplates.OrderBy(t => t.StageNumber).ToListAsync();
            var pathId = await db.LearningPaths.Where(p => p.CourseSubjectId == subjectId).Select(p => p.Id).SingleAsync();
            foreach (var level in await db.CourseLevels.OrderBy(l => l.SortOrder).ToListAsync())
            {
                var course = new Course { Code = $"PATH-{Guid.NewGuid():N}", Title = $"{level.Name} course",
                    CreatorId = example.CreatorId, TrainerId = example.TrainerId, CourseSubjectId = subjectId,
                    LearningPathId = pathId, CourseLevelId = level.SortOrder == 3 ? null : level.Id, Level = level.Name, CreditCost = 40, Status = CourseStatus.Published };
                var intake = new CourseIntake { Course = course, TrainerId = example.TrainerId, Status = CourseIntakeStatus.Completed,
                    RegistrationOpensAt = DateTime.UtcNow.AddDays(-20), RegistrationClosesAt = DateTime.UtcNow.AddDays(-10),
                    StartsAt = DateTime.UtcNow.AddDays(-9), EndsAt = DateTime.UtcNow.AddDays(-8) };
                var session = new CourseSession { CourseIntake = intake, Label = "Class", StartsAt = intake.StartsAt, EndsAt = intake.EndsAt };
                db.Enrollments.Add(new Enrollment { UserId = userId, Course = course, CourseSession = session,
                    Status = EnrollmentStatus.Completed, ProgressPercent = 100, CompletedAt = DateTime.UtcNow.AddDays(-7) });
                await db.SaveChangesAsync();
                db.UserCertificates.Add(new UserCertificate { UserId = userId, CourseId = course.Id,
                    CertificateTemplateId = templates[level.SortOrder - 1].Id, AdminApproved = true, VerificationCode = Guid.NewGuid().ToString("N") });
                await db.SaveChangesAsync();
            }
            await SeedData.InitializeAsync(db);
            await SeedData.InitializeAsync(db);
        }
        var paths = await member.GetFromJsonAsync<JsonArray>("/api/learning-paths/my");
        var path = paths!.Single(item => item!["subjectId"]!.GetValue<int>() == subjectId)!;
        Assert.True(path["graduated"]!.GetValue<bool>());
        Assert.All(path["stages"]!.AsArray(), stage => {
            Assert.True(stage!["completed"]!.GetValue<bool>());
            Assert.Equal(1, stage["completedCourseCount"]!.GetValue<int>());
        });
        using var check = factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(4, await database.UserCertificates.CountAsync(c => c.LearningPathId != null && c.User.Email == SeedData.MemberEmail));
        var certificates = await member.GetFromJsonAsync<JsonArray>("/api/certificates/my");
        var graduation = certificates!.Single(c => c!["learningPathId"]?.GetValue<int>() == path["id"]!.GetValue<int>()
            && c["stageNumber"]!.GetValue<int>() == 4)!;
        Assert.Equal("Software Development", graduation["learningPathName"]!.GetValue<string>());
        var pdf = await member.GetByteArrayAsync($"/api/certificates/{graduation["id"]!.GetValue<int>()}/file");
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Take(4).ToArray()));
        using (var change = factory.Services.CreateScope())
        {
            var db = change.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var moved = await db.Courses.SingleAsync(c => c.Title == "Advanced course");
            var marketing = await db.CourseSubjects.SingleAsync(s => s.Name == "Marketing");
            moved.CourseSubjectId = marketing.Id;
            moved.LearningPathId = await db.LearningPaths.Where(p => p.CourseSubjectId == marketing.Id).Select(p => p.Id).SingleAsync();
            await db.SaveChangesAsync();
        }
        var changedPaths = await member.GetFromJsonAsync<JsonArray>("/api/learning-paths/my");
        var marketingPath = changedPaths!.Single(p => p!["name"]!.GetValue<string>() == "Marketing")!;
        var movedCourse = marketingPath["stages"]![2]!["courses"]!.AsArray().Single(c => c!["title"]!.GetValue<string>() == "Advanced course")!;
        Assert.False(movedCourse["completed"]!.GetValue<bool>());
        Assert.True(changedPaths!.Single(p => p!["subjectId"]!.GetValue<int>() == subjectId)!["graduated"]!.GetValue<bool>());
    }

    [Fact]
    public async Task AdminCanChangeAPathStageRequirementAndMemberCannot()
    {
        using var factory = new CoLearnXApiFactory();
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        using var member = await ApiClient.AsMemberAsync(factory);
        var paths = await member.GetFromJsonAsync<JsonArray>("/api/learning-paths/my");
        var pathId = paths![0]!["id"]!.GetValue<int>();
        var url = $"/api/admin/learning-paths/{pathId}/stages/1";
        var forbidden = await member.PutAsJsonAsync(url, new { requiredCourseCount = 2 });
        Assert.True(forbidden.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
        var changed = await admin.PutAsJsonAsync(url, new { requiredCourseCount = 2 });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var invalid = await admin.PutAsJsonAsync(url, new { requiredCourseCount = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var updated = await member.GetFromJsonAsync<JsonArray>("/api/learning-paths/my");
        Assert.Equal(2, updated!.Single(path => path!["id"]!.GetValue<int>() == pathId)!["stages"]![0]!["requiredCourseCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task RecommendationsPrioritizeTheNextLevelOfACertifiedSubject()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int nextId, sameId, otherId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var userId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
            var anchor = await db.Courses.SingleAsync(c => c.Code == "INFT 2002");
            var subject = await db.CourseSubjects.SingleAsync(s => s.Name == "Software Development");
            var other = await db.CourseSubjects.SingleAsync(s => s.Name == "Marketing");
            var path = await db.LearningPaths.SingleAsync(p => p.CourseSubjectId == subject.Id);
            anchor.CourseSubjectId = subject.Id;
            anchor.LearningPathId = path.Id;
            await db.SaveChangesAsync();
            await new LearningPathService(db).ReconcileUserAsync(userId);
            var beginner = await db.CourseLevels.SingleAsync(l => l.Name == "Beginner");
            var intermediate = await db.CourseLevels.SingleAsync(l => l.Name == "Intermediate");
            Course Candidate(string title, CourseSubject courseSubject, CourseLevel level) => new() {
                Code = $"PATH-{Guid.NewGuid():N}", Title = title, CreatorId = anchor.CreatorId, TrainerId = anchor.TrainerId,
                CourseSubjectId = courseSubject.Id, CourseLevelId = level.Id, Level = level.Name, CreditCost = 40, Status = CourseStatus.Published
            };
            var next = Candidate("Next level", subject, intermediate);
            var same = Candidate("Same level", subject, beginner);
            var different = Candidate("Different subject", other, intermediate);
            db.Courses.AddRange(next, same, different);
            await db.SaveChangesAsync();
            nextId = next.Id; sameId = same.Id; otherId = different.Id;
        }
        var result = await member.GetFromJsonAsync<JsonObject>("/api/recommendations");
        var ids = result!["items"]!.AsArray().Select(item => item!["courseId"]!.GetValue<int>()).ToList();
        Assert.Contains(nextId, ids);
        Assert.Contains(sameId, ids);
        Assert.True(ids.IndexOf(nextId) < ids.IndexOf(sameId));
        Assert.DoesNotContain(otherId, ids);
    }

    [Fact]
    public async Task CourseDetailSuggestsEarlierStagesWithoutRestrictingAccess()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int courseId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var example = await db.Courses.FirstAsync();
            var subject = await db.CourseSubjects.SingleAsync(s => s.Name == "Marketing");
            var path = await db.LearningPaths.SingleAsync(p => p.CourseSubjectId == subject.Id);
            var level = await db.CourseLevels.SingleAsync(l => l.Name == "Advanced");
            var course = new Course { Code = $"PATH-{Guid.NewGuid():N}", Title = "Advanced marketing",
                CreatorId = example.CreatorId, TrainerId = example.TrainerId, CourseSubjectId = subject.Id,
                LearningPathId = path.Id, CourseLevelId = level.Id, Level = level.Name, CreditCost = 40, Status = CourseStatus.Published };
            db.Courses.Add(course);
            await db.SaveChangesAsync();
            courseId = course.Id;
        }
        var response = await member.GetAsync($"/api/courses/{courseId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("Foundation, Intermediate", detail!["suggestedPrerequisite"]!.GetValue<string>());
    }

    [Fact]
    public async Task LoweringARequirementAwardsEligibleLearnersButIncreasingItDoesNotRevokeAwards()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        int pathId, userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            userId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
            var subject = await db.CourseSubjects.SingleAsync(s => s.Name == "Software Development");
            var path = await db.LearningPaths.SingleAsync(p => p.CourseSubjectId == subject.Id);
            pathId = path.Id;
            var rule = await db.LearningPathStages.SingleAsync(s => s.LearningPathId == path.Id && s.StageNumber == 1);
            rule.RequiredCourseCount = 2;
            var course = await db.Courses.SingleAsync(c => c.Code == "INFT 2002");
            course.CourseSubjectId = subject.Id;
            course.LearningPathId = path.Id;
            await db.SaveChangesAsync();
            await new LearningPathService(db).ReconcileUserAsync(userId);
            Assert.Equal(0, await db.UserCertificates.CountAsync(c => c.UserId == userId && c.LearningPathId == pathId));
        }
        var endpoint = $"/api/admin/learning-paths/{pathId}/stages/1";
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(endpoint, new { requiredCourseCount = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(endpoint, new { requiredCourseCount = 3 })).StatusCode);
        var paths = await member.GetFromJsonAsync<JsonArray>("/api/learning-paths/my");
        var stage = paths!.Single(p => p!["id"]!.GetValue<int>() == pathId)!["stages"]![0]!;
        Assert.True(stage["completed"]!.GetValue<bool>());
        Assert.Equal(3, stage["requiredCourseCount"]!.GetValue<int>());
        using var check = factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.Equal(1, await database.UserCertificates.CountAsync(c => c.UserId == userId && c.LearningPathId == pathId));
    }

    [Fact]
    public async Task AnIssuedCertificateCountsOnlyWithACompletedEnrollment()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/learning-paths/my")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.GetAsync("/api/learning-paths/my")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var userId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
        var subject = await db.CourseSubjects.SingleAsync(s => s.Name == "Marketing");
        var path = await db.LearningPaths.SingleAsync(p => p.CourseSubjectId == subject.Id);
        var course = await db.Courses.SingleAsync(c => c.Code == "INFT 2051");
        course.CourseSubjectId = subject.Id;
        course.LearningPathId = path.Id;
        var enrollment = await db.Enrollments.SingleAsync(e => e.UserId == userId && e.CourseId == course.Id);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
        db.UserCertificates.Add(new UserCertificate { UserId = userId, CourseId = course.Id, AdminApproved = true,
            CertificateTemplateId = await db.CertificateTemplates.Where(t => t.StageNumber == 1).Select(t => t.Id).SingleAsync(),
            VerificationCode = Guid.NewGuid().ToString("N") });
        await db.SaveChangesAsync();
        var service = new LearningPathService(db);
        await service.ReconcileUserAsync(userId);
        Assert.Equal(0, await db.LearningPathCompletions.CountAsync(c => c.UserId == userId && c.LearningPathId == path.Id));
        enrollment.Status = EnrollmentStatus.Completed;
        enrollment.ProgressPercent = 100;
        await db.SaveChangesAsync();
        await service.ReconcileUserAsync(userId);
        Assert.Equal(1, await db.LearningPathCompletions.CountAsync(c => c.UserId == userId && c.LearningPathId == path.Id));
    }
}
