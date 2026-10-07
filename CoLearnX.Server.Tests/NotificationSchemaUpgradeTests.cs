using CoLearnX.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class NotificationSchemaUpgradeTests
{
    [Fact]
    public async Task Existing_sqlite_notifications_gain_nullable_enrollment_id_without_losing_rows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE Notifications (
                Id INTEGER PRIMARY KEY,
                UserId INTEGER NOT NULL,
                Code TEXT NOT NULL,
                Title TEXT NOT NULL,
                Body TEXT NOT NULL,
                IsRead INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                IntakeId INTEGER NULL
            );
            INSERT INTO Notifications (Id, UserId, Code, Title, Body, IsRead, CreatedAt, IntakeId)
            VALUES (1, 5, 'N-01', 'Legacy', 'Legacy row', 0, '2026-10-05T00:00:00Z', 9);
            """);

        await NotificationSchema.EnsureAsync(db);
        await NotificationSchema.EnsureAsync(db);

        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Notifications").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Notifications') WHERE name = 'EnrollmentId'").SingleAsync());
        Assert.Null(await db.Database.SqlQueryRaw<int?>("SELECT EnrollmentId AS Value FROM Notifications WHERE Id = 1").SingleAsync());
    }
}
