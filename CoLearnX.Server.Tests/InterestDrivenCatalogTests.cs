using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

// A Member's interests decide which courses they see first: interests map to one of the 12 course subjects.
public class InterestDrivenCatalogTests
{
    [Theory]
    [InlineData("development", "web-development", "Software Development")]
    [InlineData("it-software", "cybersecurity", "Cybersecurity and Infrastructure")]
    [InlineData("data-ai", "machine-learning", "Data and Artificial Intelligence")]
    [InlineData("design", "ux-research", "Digital Product Design")]
    [InlineData("design", "graphic-design", "Media Production")]
    [InlineData("business", "project-management", "Management")]
    [InlineData("business", "sales", "Marketing")]
    [InlineData("finance-accounting", "accounting", "Accounting and Finance")]
    [InlineData("office-productivity", "microsoft-office", "Organizational Operations")]
    [InlineData("health-fitness", "nutrition", "Health and Care")]
    [InlineData("language-learning", "english", "Languages")]
    [InlineData("teaching-academics", "math", "Education and Academic Foundations")]
    public void Interests_map_to_one_of_the_twelve_subjects(string parent, string leaf, string subject)
        => Assert.Equal(subject, InterestSubjectMap.SubjectFor(parent, leaf));

    [Fact]
    public async Task Every_seeded_interest_maps_to_a_seeded_subject_in_the_same_area()
    {
        using var factory = new CoLearnXApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var subjects = await db.CourseSubjects.ToDictionaryAsync(item => item.Name, item => item.Subject);
        var leaves = await db.Interests.Where(item => item.ParentId != null).Include(item => item.Parent).ToListAsync();

        Assert.NotEmpty(leaves);
        foreach (var leaf in leaves)
        {
            var name = InterestSubjectMap.SubjectFor(leaf.Parent!.Slug, leaf.Slug);
            Assert.True(name is not null && subjects.ContainsKey(name), $"{leaf.Slug} has no subject");
            Assert.Equal(InterestSubjectMap.For(leaf.Parent.Slug, leaf.Slug), subjects[name!]);
        }
    }

    [Fact]
    public async Task Catalog_puts_courses_matching_the_members_interests_first()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int tagMatch, subjectMatch;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            await SetInterestsAsync(db, "web-development");
            tagMatch = await AddCourseAsync(db, "ZZZ 901", "web-development", "Software Development", open: false);
            // No shared tag, but devops sits in the same subject as web-development.
            subjectMatch = await AddCourseAsync(db, "ZZZ 902", "devops", "Software Development", open: false);
        }

        var courses = await member.GetFromJsonAsync<JsonArray>("/api/courses");

        var matched = courses!.TakeWhile(item => item!["matchesInterests"]!.GetValue<bool>())
            .Select(item => item!["id"]!.GetValue<int>()).ToList();
        Assert.Contains(tagMatch, matched);
        Assert.Contains(subjectMatch, matched);
        Assert.Contains(courses!, item => !item!["matchesInterests"]!.GetValue<bool>());
        Assert.DoesNotContain(courses!.Skip(matched.Count), item => item!["matchesInterests"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Recommendations_include_courses_without_an_open_class_and_mark_them()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        int notOpen;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            await ColdStartAsync(db);
            await SetInterestsAsync(db, "web-development");
            notOpen = await AddCourseAsync(db, "REC 911", "web-development", "Software Development", open: false);
        }

        var response = await member.GetFromJsonAsync<JsonObject>("/api/recommendations");

        var item = Assert.Single(response!["items"]!.AsArray(), item => item!["courseId"]!.GetValue<int>() == notOpen);
        Assert.False(item!["hasOpenClass"]!.GetValue<bool>());
    }

    private static async Task ColdStartAsync(CoLearnXDbContext db)
    {
        var memberId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
        foreach (var enrollment in await db.Enrollments.Where(e => e.UserId == memberId).ToListAsync())
            enrollment.Status = EnrollmentStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    private static async Task SetInterestsAsync(CoLearnXDbContext db, params string[] slugs)
    {
        var memberId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
        db.UserInterests.RemoveRange(db.UserInterests.Where(i => i.UserId == memberId));
        var ids = await db.Interests.Where(i => slugs.Contains(i.Slug)).Select(i => i.Id).ToListAsync();
        db.UserInterests.AddRange(ids.Select(id => new UserInterest { UserId = memberId, InterestId = id }));
        await db.SaveChangesAsync();
    }

    private static async Task<int> AddCourseAsync(CoLearnXDbContext db, string code, string tagSlugs, string subject, bool open)
    {
        var template = await db.Courses.SingleAsync(c => c.Code == "INFT 2002");
        var beginner = await db.CourseLevels.SingleAsync(l => l.Name == "Beginner");
        var course = new Course
        {
            Code = code, Title = code, TrainerId = template.TrainerId, CreatorId = template.CreatorId, CreditCost = 20,
            CourseLevelId = beginner.Id, LearningPathId = template.LearningPathId, Level = beginner.Name,
            Category = template.Category, Status = CourseStatus.Published,
            CourseSubjectId = await db.CourseSubjects.Where(s => s.Name == subject).Select(s => s.Id).SingleAsync(),
        };
        var slugs = tagSlugs.Split(' ');
        foreach (var id in await db.Interests.Where(i => slugs.Contains(i.Slug)).Select(i => i.Id).ToListAsync())
            course.Interests.Add(new CourseInterest { InterestId = id });
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        if (open)
        {
            var start = DateTime.UtcNow.AddDays(15);
            var intake = new CourseIntake { CourseId = course.Id, TrainerId = course.TrainerId,
                RegistrationOpensAt = DateTime.UtcNow.AddDays(-1), RegistrationClosesAt = DateTime.UtcNow.AddDays(10),
                StartsAt = start, EndsAt = start.AddDays(1), Status = CourseIntakeStatus.Published };
            intake.Sessions.Add(new CourseSession { Label = "Online", StartsAt = start, EndsAt = start.AddHours(2),
                PhysicalCapacity = 0, MeetingLink = "https://example.com/meeting" });
            db.CourseIntakes.Add(intake);
            await db.SaveChangesAsync();
        }
        return course.Id;
    }
}
