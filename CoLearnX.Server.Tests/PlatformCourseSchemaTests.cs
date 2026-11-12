using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public sealed class PlatformCourseSchemaTests
{
    [Fact]
    public async Task Upgrades_a_legacy_sqlite_schema_for_platform_courses_without_bringing_staff_back()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE Courses (Id INTEGER PRIMARY KEY AUTOINCREMENT);
                CREATE TABLE AuditLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AdminAccountId INTEGER NULL,
                    UserId INTEGER NULL,
                    Action TEXT NOT NULL,
                    EntityType TEXT NOT NULL,
                    EntityId TEXT NULL,
                    Result TEXT NOT NULL,
                    Reason TEXT NULL,
                    CreatedAt TEXT NOT NULL);
                """;
            await command.ExecuteNonQueryAsync();
        }
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options;
        await using var db = new CoLearnXDbContext(options);

        await PlatformCourseSchema.EnsureAsync(db);
        await PlatformCourseSchema.EnsureAsync(db);

        Assert.Equal(1, await ScalarAsync(connection,
            "SELECT COUNT(*) FROM pragma_table_info('Courses') WHERE name='IsPlatformOwned'"));
        Assert.Equal(0, await ScalarAsync(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='StaffAccounts'"));
        Assert.Equal(0, await ScalarAsync(connection,
            "SELECT COUNT(*) FROM pragma_table_info('AuditLogs') WHERE name='StaffAccountId'"));
    }

    [Fact]
    public async Task A_new_database_has_no_staff_tables_or_columns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options;
        await using var db = new CoLearnXDbContext(options);
        await db.Database.EnsureCreatedAsync();

        Assert.Equal(0, await ScalarAsync(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='StaffAccounts'"));
        Assert.Equal(0, await ScalarAsync(connection,
            "SELECT COUNT(*) FROM pragma_table_info('AuditLogs') WHERE name='StaffAccountId'"));
    }

    [Fact]
    public async Task A_database_that_still_has_the_old_staff_column_keeps_accepting_audit_rows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options;
        await using var db = new CoLearnXDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE AuditLogs ADD COLUMN StaffAccountId INTEGER NULL");
        var user = new User { Email = "legacy-audit@example.com", FullName = "Legacy Audit" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        db.AuditLogs.Add(new AuditLog { UserId = user.Id, Action = "Test", EntityType = "User", Result = "Succeeded" });
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.UserId == user.Id));
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }
}
