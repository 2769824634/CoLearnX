using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public sealed class RecommendationDemoSeedTests
{
    [Fact]
    public async Task Fresh_seed_normalizes_huang_profile_and_adds_completed_demo_ratings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"colearnx-recommendation-seed-{Guid.NewGuid():N}.db");
        try
        {
            await using (var db = NewDb(path))
            {
                await SeedData.InitializeAsync(db);
            }

            await using var check = NewDb(path);
            var huang = await check.Users.SingleAsync(user => user.Email == SeedData.MemberEmail);
            var preference = await check.UserPreferences.SingleAsync(item => item.UserId == huang.Id);
            Assert.Equal("professional", preference.LearningGoals);
            Assert.NotNull(preference.OnboardingCompletedAt);

            var interests = await check.UserInterests.Where(item => item.UserId == huang.Id)
                .OrderBy(item => item.Interest.Slug)
                .Select(item => item.Interest.Slug)
                .ToListAsync();
            Assert.Equal(["cybersecurity", "user-experience-design"], interests);

            var demoRatings = await check.ProgramRatings
                .Include(rating => rating.Enrollment)
                .ThenInclude(enrollment => enrollment.Course)
                .Include(rating => rating.Enrollment)
                .ThenInclude(enrollment => enrollment.User)
                .Where(rating => rating.Comment != null && rating.Comment.StartsWith("Demo seed rating:"))
                .ToListAsync();
            Assert.Equal(6, demoRatings.Count);
            Assert.Equal(3, demoRatings.Count(rating => rating.Enrollment.Course.Code == "INFT 2051"));
            Assert.Equal(3, demoRatings.Count(rating => rating.Enrollment.Course.Code == "INFT 2002"));
            Assert.All(demoRatings, rating => Assert.Equal(EnrollmentStatus.Completed, rating.Enrollment.Status));
            Assert.All(demoRatings, rating => Assert.StartsWith("Demo Reviewer ", rating.Enrollment.User.FullName));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Repeated_seed_is_idempotent_and_preserves_member_changes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"colearnx-recommendation-seed-{Guid.NewGuid():N}.db");
        try
        {
            await using (var first = NewDb(path))
            {
                await SeedData.InitializeAsync(first);
            }

            await using (var changed = NewDb(path))
            {
                var huang = await changed.Users.SingleAsync(user => user.Email == SeedData.MemberEmail);
                var preference = await changed.UserPreferences.SingleAsync(item => item.UserId == huang.Id);
                preference.LearningGoals = "hobby";
                changed.UserInterests.RemoveRange(changed.UserInterests.Where(item => item.UserId == huang.Id));
                var customInterest = await changed.Interests.SingleAsync(item => item.Slug == "ux-research");
                changed.UserInterests.Add(new UserInterest { UserId = huang.Id, InterestId = customInterest.Id });
                var previous = await changed.Enrollments.FirstAsync(e => e.User.Email == "demo.reviewer.ux@colearnx.com");
                changed.Enrollments.Add(new Enrollment { UserId = previous.UserId, CourseId = previous.CourseId,
                    CourseSessionId = previous.CourseSessionId, Status = EnrollmentStatus.Cancelled });
                await changed.SaveChangesAsync();
            }

            await using (var second = NewDb(path))
            {
                await SeedData.InitializeAsync(second);
            }

            await using var check = NewDb(path);
            var huangAfterRerun = await check.Users.SingleAsync(user => user.Email == SeedData.MemberEmail);
            var preferenceAfterRerun = await check.UserPreferences.SingleAsync(item => item.UserId == huangAfterRerun.Id);
            Assert.Equal("hobby", preferenceAfterRerun.LearningGoals);
            Assert.Equal(["ux-research"], await check.UserInterests.Where(item => item.UserId == huangAfterRerun.Id)
                .Select(item => item.Interest.Slug).ToListAsync());
            Assert.Equal(6, await check.ProgramRatings.CountAsync(rating =>
                rating.Comment != null && rating.Comment.StartsWith("Demo seed rating:")));
            Assert.Equal(6, await check.Enrollments.CountAsync(enrollment =>
                enrollment.User.Email.StartsWith("demo.reviewer.") && enrollment.Status == EnrollmentStatus.Completed));
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    [Fact]
    public async Task Custom_member_goal_without_interests_is_not_treated_as_legacy_seed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"colearnx-recommendation-seed-{Guid.NewGuid():N}.db");
        try
        {
            await using (var first = NewDb(path))
            {
                await SeedData.InitializeAsync(first);
            }

            await using (var changed = NewDb(path))
            {
                var huang = await changed.Users.SingleAsync(user => user.Email == SeedData.MemberEmail);
                var preference = await changed.UserPreferences.SingleAsync(item => item.UserId == huang.Id);
                preference.LearningGoals = "my own learning plan";
                changed.UserInterests.RemoveRange(changed.UserInterests.Where(item => item.UserId == huang.Id));
                await changed.SaveChangesAsync();
            }

            await using (var second = NewDb(path))
            {
                await SeedData.InitializeAsync(second);
            }

            await using var check = NewDb(path);
            var huangAfterRerun = await check.Users.SingleAsync(user => user.Email == SeedData.MemberEmail);
            var preferenceAfterRerun = await check.UserPreferences.SingleAsync(item => item.UserId == huangAfterRerun.Id);
            Assert.Equal("my own learning plan", preferenceAfterRerun.LearningGoals);
            Assert.Empty(await check.UserInterests.Where(item => item.UserId == huangAfterRerun.Id).ToListAsync());
        }
        finally
        {
            DeleteDatabase(path);
        }
    }

    private static CoLearnXDbContext NewDb(string path) => new(new DbContextOptionsBuilder<CoLearnXDbContext>()
        .UseSqlite($"Data Source={path}")
        .Options);

    private static void DeleteDatabase(string path)
    {
        foreach (var file in new[] { path, path + "-wal", path + "-shm" })
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }
}
