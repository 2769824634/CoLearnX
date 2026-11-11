using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class SeedDataTests
{
    [Fact]
    public async Task Initialize_disables_existing_staff_user_but_preserves_staff_history()
    {
        var path = Path.Combine(Path.GetTempPath(), $"colearnx-staff-retire-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite($"Data Source={path}").Options;
        try
        {
            int staffId;
            int userId;
            await using (var seeded = new CoLearnXDbContext(options))
            {
                await SeedData.InitializeAsync(seeded);
                var admin = await seeded.AdminAccounts.SingleAsync(item => item.Email == SeedData.AdminEmail);
                var hidden = new User
                {
                    Email = "retired-staff@staff.colearnx", PasswordHash = "x", FullName = "Retired staff",
                    DisplayName = "Retired staff", IsActive = true, EmailVerifiedAt = DateTime.UtcNow,
                };
                seeded.Users.Add(hidden);
                await seeded.SaveChangesAsync();
                var staff = new StaffAccount
                {
                    Email = "retired@colearnx.test", PasswordHash = "x", FullName = "Retired staff",
                    DisplayName = "Retired staff", StaffNumber = 99, DocumentPath = "retired.pdf",
                    UserId = hidden.Id, CreatedByAdminAccountId = admin.Id,
                };
                seeded.StaffAccounts.Add(staff);
                await seeded.SaveChangesAsync();
                staffId = staff.Id;
                userId = hidden.Id;
            }

            await using (var repaired = new CoLearnXDbContext(options)) await SeedData.InitializeAsync(repaired);

            await using var db = new CoLearnXDbContext(options);
            Assert.True(await db.StaffAccounts.AnyAsync(item => item.Id == staffId));
            Assert.False((await db.Users.SingleAsync(item => item.Id == userId)).IsActive);
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
            Assert.False(await db.RoleRequests.AnyAsync(request =>
                !db.UserRoles.Any(role => role.UserId == request.UserId && role.Role == request.RequestedRole)));
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + "-wal");
            TryDelete(path + "-shm");
        }
    }

    [Fact]
    public async Task Initialize_repairs_demo_accounts_to_one_role_each_and_rejects_stale_role_requests()
    {
        var path = Path.Combine(Path.GetTempPath(), $"colearnx-seed-single-role-{Guid.NewGuid():N}.db");
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
                var member = await seeded.Users.SingleAsync(user => user.Email == SeedData.MemberEmail);
                seeded.UserRoles.AddRange(
                    new UserRole { UserId = trainer.Id, Role = AppRole.Member },
                    new UserRole { UserId = creator.Id, Role = AppRole.Member });
                // A pending request from the old "Member applies for another role" flow.
                seeded.RoleRequests.Add(new RoleRequest { UserId = member.Id, RequestedRole = AppRole.Trainer });
                await seeded.SaveChangesAsync();
            }

            await using (var cleaned = new CoLearnXDbContext(options))
            {
                await SeedData.InitializeAsync(cleaned);
            }

            await using var db = new CoLearnXDbContext(options);
            await AssertDemoWorkspaceRolesAsync(db);
            Assert.False(await db.RoleRequests.AnyAsync(request => request.Status == RoleRequestStatus.Pending
                && !db.UserRoles.Any(role => role.UserId == request.UserId && role.Role == request.RequestedRole)));
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
        Assert.Equal(new[] { AppRole.Trainer }, await RolesAsync(SeedData.TrainerEmail));
        Assert.Equal(new[] { AppRole.Creator }, await RolesAsync(SeedData.CreatorEmail));
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
