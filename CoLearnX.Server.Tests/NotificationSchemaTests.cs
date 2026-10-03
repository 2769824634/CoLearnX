using CoLearnX.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class NotificationSchemaTests
{
    [Fact]
    public async Task LegacyNotificationsUpgradeIdempotentlyWithoutSendingHistoricalMail()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Notifications (Id INTEGER PRIMARY KEY, Body TEXT); INSERT INTO Notifications VALUES (1, 'Existing message');");
        await NotificationSchema.EnsureAsync(db);
        await NotificationSchema.EnsureAsync(db);
        Assert.Equal("Existing message", await db.Database.SqlQueryRaw<string>("SELECT Body AS Value FROM Notifications WHERE Id = 1").SingleAsync());
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT EmailPending AS Value FROM Notifications WHERE Id = 1").SingleAsync());
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Notifications WHERE IntakeId IS NOT NULL OR EmailSentAt IS NOT NULL OR EmailAttemptedAt IS NOT NULL").SingleAsync());
    }
}
