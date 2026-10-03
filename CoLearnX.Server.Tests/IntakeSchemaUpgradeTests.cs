using CoLearnX.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class IntakeSchemaUpgradeTests
{
    [Fact]
    public async Task ExistingSqliteTablesAreUpgradedInPlaceAndRepeatedUpgradePreservesData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE Users (Id INTEGER PRIMARY KEY, CreditBalance INTEGER NOT NULL);
            CREATE TABLE CourseIntakes (Id INTEGER PRIMARY KEY);
            CREATE TABLE Enrollments (Id INTEGER PRIMARY KEY);
            CREATE TABLE CreditTransactions (Id INTEGER PRIMARY KEY, RelatedEnrollmentId INTEGER, Type INTEGER NOT NULL);
            INSERT INTO Users VALUES (1, 83);
            INSERT INTO CourseIntakes VALUES (1);
            INSERT INTO Enrollments VALUES (1);
            """);
        await CreditReservationSchema.EnsureAsync(db);
        await CreditReservationSchema.EnsureAsync(db);
        Assert.Equal(83, await db.Database.SqlQueryRaw<int>("SELECT CreditBalance AS Value FROM Users WHERE Id = 1").SingleAsync());
        Assert.Equal(10, await db.Database.SqlQueryRaw<int>("SELECT MinEnrollment AS Value FROM CourseIntakes WHERE Id = 1").SingleAsync());
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT PostponementEligible AS Value FROM Enrollments WHERE Id = 1").SingleAsync());
        await db.Database.ExecuteSqlRawAsync("INSERT INTO CourseIntakes (Id, ReplacementForIntakeId) VALUES (2, 1)");
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("INSERT INTO CourseIntakes (Id, ReplacementForIntakeId) VALUES (3, 1)"));
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Enrollments (Id, PostponedFromEnrollmentId) VALUES (2, 1)");
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("INSERT INTO Enrollments (Id, PostponedFromEnrollmentId) VALUES (3, 1)"));
    }
}
