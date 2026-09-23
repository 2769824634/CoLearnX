using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class SeedDataTests
{
    [Fact]
    public async Task Initialize_preserves_team_accounts_and_separates_admin_identity()
    {
        var path = Path.Combine(Path.GetTempPath(), $"colearnx-seed-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        try
        {
            await using (var seeded = new CoLearnXDbContext(options))
            {
                await SeedData.InitializeAsync(seeded);
            }

            await using var db = new CoLearnXDbContext(options);
            Assert.False(await db.Users.AnyAsync(u => u.Email == "jane.smith@colearnx.com"));
            Assert.True(await db.Users.AnyAsync(u => u.Email == SeedData.TrainerEmail));
            Assert.True(await db.Users.AnyAsync(u => u.Email == SeedData.MemberEmail));
            Assert.True(await db.Users.AnyAsync(u => u.Email == SeedData.CreatorEmail));
            Assert.False(await db.Users.AnyAsync(u => u.Email == SeedData.AdminEmail));
            Assert.True(await db.AdminAccounts.AnyAsync(a => a.Email == SeedData.AdminEmail));
            var course = await db.Courses.Include(item => item.CourseLevel).Include(item => item.LearningPath)
                .SingleAsync(item => item.Code == "INFT 2051");
            Assert.Equal("Beginner", course.CourseLevel!.Name);
            Assert.Equal("Design", course.LearningPath!.Name);
            Assert.Equal(
                new[] { "Beginner", "Intermediate", "Advanced" },
                await db.CourseLevels.OrderBy(item => item.SortOrder).Select(item => item.Name).ToListAsync());
            await AssertDemoWorkspaceRolesAsync(db);
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + "-wal");
            TryDelete(path + "-shm");
        }
    }

    [Fact]
    public async Task Initialize_repairs_missing_member_role_on_existing_trainer_and_creator()
    {
        var path = Path.Combine(Path.GetTempPath(), $"colearnx-seed-repair-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        try
        {
            await using (var seeded = new CoLearnXDbContext(options))
            {
                await SeedData.InitializeAsync(seeded);
                var trainer = await seeded.Users.SingleAsync(user => user.Email == SeedData.TrainerEmail);
                var creator = await seeded.Users.SingleAsync(user => user.Email == SeedData.CreatorEmail);
                seeded.UserRoles.RemoveRange(seeded.UserRoles.Where(role =>
                    (role.UserId == trainer.Id || role.UserId == creator.Id) && role.Role == AppRole.Member));
                await seeded.SaveChangesAsync();
            }

            await using (var repaired = new CoLearnXDbContext(options))
            {
                await SeedData.InitializeAsync(repaired);
            }

            await using var db = new CoLearnXDbContext(options);
            await AssertDemoWorkspaceRolesAsync(db);
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + "-wal");
            TryDelete(path + "-shm");
        }
    }

    private static async Task AssertDemoWorkspaceRolesAsync(CoLearnXDbContext db)
    {
        async Task<AppRole[]> RolesAsync(string email) =>
            await db.UserRoles.Where(role => role.User.Email == email)
                .Select(role => role.Role)
                .OrderBy(role => role)
                .ToArrayAsync();

        Assert.Equal(new[] { AppRole.Member }, await RolesAsync(SeedData.MemberEmail));
        Assert.Equal(new[] { AppRole.Member, AppRole.Trainer }, await RolesAsync(SeedData.TrainerEmail));
        Assert.Equal(new[] { AppRole.Member, AppRole.Creator }, await RolesAsync(SeedData.CreatorEmail));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
