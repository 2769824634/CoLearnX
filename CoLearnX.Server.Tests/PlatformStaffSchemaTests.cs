using CoLearnX.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public sealed class PlatformStaffSchemaTests
{
    [Fact]
    public async Task Upgrades_legacy_sqlite_schema_for_staff_platform_courses_and_audit_actor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                PRAGMA foreign_keys = OFF;
                CREATE TABLE AdminAccounts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Email TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    IsActive INTEGER NOT NULL,
                    SessionStamp TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL);
                CREATE TABLE Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Email TEXT NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    FullName TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    Phone TEXT NULL,
                    CreditBalance INTEGER NOT NULL,
                    HeldCredits INTEGER NOT NULL,
                    IsActive INTEGER NOT NULL,
                    SessionStamp TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL);
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
                    CreatedAt TEXT NOT NULL,
                    CONSTRAINT CK_AuditLogs_ExactlyOneActor CHECK (
                        (AdminAccountId IS NOT NULL AND UserId IS NULL)
                        OR (AdminAccountId IS NULL AND UserId IS NOT NULL)));
                """;
            await command.ExecuteNonQueryAsync();
        }
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options;
        await using var db = new CoLearnXDbContext(options);

        await PlatformStaffSchema.EnsureAsync(db);

        Assert.Equal(1, await ScalarAsync(connection,
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='StaffAccounts'"));
        Assert.Equal(1, await ScalarAsync(connection,
            "SELECT COUNT(*) FROM pragma_table_info('Courses') WHERE name='IsPlatformOwned'"));
        Assert.Equal(1, await ScalarAsync(connection,
            "SELECT COUNT(*) FROM pragma_table_info('AuditLogs') WHERE name='StaffAccountId'"));
        Assert.Contains("CASE WHEN StaffAccountId IS NULL", await TextAsync(connection,
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='AuditLogs'"));
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<string> TextAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync() ?? string.Empty);
    }
}
