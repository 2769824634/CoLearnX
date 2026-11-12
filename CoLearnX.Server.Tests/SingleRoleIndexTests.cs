using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

// One account holds one role, enforced by a unique index on UserRoles(UserId).
public sealed class SingleRoleIndexTests
{
    [Fact]
    public async Task New_database_rejects_a_second_role_for_the_same_user()
    {
        await using var connection = await OpenAsync();
        await using var db = await CreatedAsync(connection);
        var userId = await AddUserAsync(db, "one@example.com", AppRole.Trainer);

        db.UserRoles.Add(new UserRole { UserId = userId, Role = AppRole.Creator });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Existing_database_with_single_role_accounts_gets_the_index()
    {
        await using var connection = await OpenAsync();
        await using var db = await CreatedAsync(connection);
        var userId = await AddUserAsync(db, "one@example.com", AppRole.Trainer);
        await AddUserAsync(db, "two@example.com", AppRole.Member);
        await db.Database.ExecuteSqlRawAsync("DROP INDEX UX_UserRoles_UserId");

        var duplicates = await RoleCertificationSchema.EnsureSingleRoleIndexAsync(db);
        var again = await RoleCertificationSchema.EnsureSingleRoleIndexAsync(db);

        Assert.Empty(duplicates);
        Assert.Empty(again);
        Assert.True(await IndexExistsAsync(db));
        db.UserRoles.Add(new UserRole { UserId = userId, Role = AppRole.Creator });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Existing_database_with_multi_role_accounts_skips_the_index_without_failing()
    {
        await using var connection = await OpenAsync();
        await using var db = await CreatedAsync(connection);
        await db.Database.ExecuteSqlRawAsync("DROP INDEX UX_UserRoles_UserId");
        var userId = await AddUserAsync(db, "one@example.com", AppRole.Trainer);
        db.UserRoles.Add(new UserRole { UserId = userId, Role = AppRole.Creator });
        await db.SaveChangesAsync();

        var duplicates = await RoleCertificationSchema.EnsureSingleRoleIndexAsync(db);

        Assert.Equal([userId], duplicates);
        Assert.False(await IndexExistsAsync(db));
    }

    private static async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<CoLearnXDbContext> CreatedAsync(SqliteConnection connection)
    {
        var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static async Task<int> AddUserAsync(CoLearnXDbContext db, string email, AppRole role)
    {
        var user = new User { Email = email, PasswordHash = "hash" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, Role = role });
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<bool> IndexExistsAsync(CoLearnXDbContext db) =>
        await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'index' AND name = 'UX_UserRoles_UserId'")
            .SingleAsync() == 1;
}
