using CoLearnX.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public sealed class SessionMaterialSchemaUpgradeTests
{
    [Fact]
    public async Task Existing_sqlite_database_gains_session_materials_and_repeated_upgrade_is_safe()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>()
            .UseSqlite(connection).Options);

        // This is the minimum v8 shape needed by the new foreign key.
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE CourseSessions (Id INTEGER PRIMARY KEY); INSERT INTO CourseSessions (Id) VALUES (1);");

        await SessionMaterialSchema.EnsureAsync(db);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO SessionMaterials (CourseSessionId, AddedByTrainerId, Title, FilePath, Format, UploadedAt) VALUES (1, 7, 'Handout', 'session-materials/7/1/file.pdf', 'PDF', '2026-10-05T00:00:00Z');");
        await SessionMaterialSchema.EnsureAsync(db);

        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM SessionMaterials").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('SessionMaterials') WHERE name = 'FilePath'").SingleAsync());
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_index_list('SessionMaterials') WHERE name = 'IX_SessionMaterials_CourseSessionId_UploadedAt'").SingleAsync());
    }
}
