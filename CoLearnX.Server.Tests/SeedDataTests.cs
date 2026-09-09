using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
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
        }
        finally
        {
            TryDelete(path);
            TryDelete(path + "-wal");
            TryDelete(path + "-shm");
        }
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
