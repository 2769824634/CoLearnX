using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class SeedDataTests
{
    [Fact]
    public async Task Initialize_seeds_local_staff_demo_only_when_demo_admin_is_enabled()
    {
        async Task<bool> SeedAndFindAsync(bool demoEnabled)
        {
            var path = Path.Combine(Path.GetTempPath(), $"colearnx-staff-seed-{Guid.NewGuid():N}.db");
            var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite($"Data Source={path}").Options;
            try
            {
                await using var db = new CoLearnXDbContext(options);
                await SeedData.InitializeAsync(db, seedDemoAdmin: demoEnabled, seedDemoStaff: demoEnabled);
                var account = await db.StaffAccounts.SingleOrDefaultAsync(item => item.Email == SeedData.StaffEmail);
                if (account is null) return false;
                Assert.True(account.IsActive);
                Assert.Equal("qingkong", account.FullName);
                Assert.Equal("qingkong", account.DisplayName);
                Assert.True(BCrypt.Net.BCrypt.Verify(SeedData.DemoPassword, account.PasswordHash));
                Assert.True(await db.UserRoles.AnyAsync(role => role.UserId == account.UserId && role.Role == AppRole.Trainer && !role.IsVisible));
                return true;
            }
            finally
            {
                TryDelete(path); TryDelete(path + "-wal"); TryDelete(path + "-shm");
            }
        }

        Assert.True(await SeedAndFindAsync(true));
        Assert.False(await SeedAndFindAsync(false));
    }

    [Fact]
    public async Task Initialize_repairs_the_existing_local_staff_name()
    {
        var path = Path.Combine(Path.GetTempPath(), $"colearnx-staff-name-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite($"Data Source={path}").Options;
        try
        {
            await using (var seeded = new CoLearnXDbContext(options))
            {
                await SeedData.InitializeAsync(seeded, seedDemoAdmin: true, seedDemoStaff: true);
                var account = await seeded.StaffAccounts.Include(item => item.User)
                    .SingleAsync(item => item.Email == SeedData.StaffEmail);
                account.FullName = "Qing Kong";
                account.DisplayName = "CoLearnX 讲师 001";
                account.User.FullName = "Qing Kong";
                account.User.DisplayName = "CoLearnX 讲师 001";
                await seeded.SaveChangesAsync();
            }

            await using (var repaired = new CoLearnXDbContext(options))
                await SeedData.InitializeAsync(repaired, seedDemoAdmin: true, seedDemoStaff: true);

            await using var db = new CoLearnXDbContext(options);
            var updated = await db.StaffAccounts.Include(item => item.User)
                .SingleAsync(item => item.Email == SeedData.StaffEmail);
            Assert.Equal("qingkong", updated.FullName);
            Assert.Equal("qingkong", updated.DisplayName);
            Assert.Equal("qingkong", updated.User.FullName);
            Assert.Equal("qingkong", updated.User.DisplayName);
        }
        finally
        {
            TryDelete(path); TryDelete(path + "-wal"); TryDelete(path + "-shm");
        }
    }

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
