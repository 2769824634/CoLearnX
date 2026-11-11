using CoLearnX.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class LearningPathSchemaUpgradeTests
{
    [Fact]
    public async Task ExistingSqliteTablesUpgradeTwiceWithoutDiscardingLegacyRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE CourseSubjects (Id INTEGER PRIMARY KEY);
            CREATE TABLE Users (Id INTEGER PRIMARY KEY);
            CREATE TABLE Courses (Id INTEGER PRIMARY KEY);
            CREATE TABLE LearningPaths (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Description TEXT NULL);
            CREATE TABLE UserCertificates (Id INTEGER PRIMARY KEY, UserId INTEGER NOT NULL, CertificateTemplateId INTEGER NOT NULL);
            INSERT INTO LearningPaths (Id, Name) VALUES (7, 'Legacy');
            INSERT INTO UserCertificates (Id, UserId, CertificateTemplateId) VALUES (9, 2, 1);
            """);
        await LearningPathSchema.EnsureAsync(db);
        await LearningPathSchema.EnsureAsync(db);
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM LearningPaths WHERE Id = 7 AND Name = 'Legacy'").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM UserCertificates WHERE Id = 9 AND LearningPathId IS NULL").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('LearningPaths') WHERE name = 'CourseSubjectId'").SingleAsync());
        Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name IN ('LearningPathStages', 'LearningPathCompletions')").SingleAsync());
    }
}
