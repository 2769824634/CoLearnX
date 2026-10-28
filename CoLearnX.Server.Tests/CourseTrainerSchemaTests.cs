using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public sealed class CourseTrainerSchemaTests
{
    [Fact]
    public async Task Existing_sqlite_database_gains_the_roster_table_matching_a_new_database()
    {
        var (upgradedConnection, upgraded) = await OpenAsync();
        await using var upgradedScope = upgradedConnection;
        await using var upgradedDb = upgraded;
        await upgraded.Database.EnsureCreatedAsync();
        await upgraded.Database.ExecuteSqlRawAsync("DROP TABLE CourseTrainers");

        await CourseTrainerSchema.EnsureAsync(upgraded);
        await CourseTrainerSchema.EnsureAsync(upgraded);

        var (createdConnection, created) = await OpenAsync();
        await using var createdScope = createdConnection;
        await using var createdDb = created;
        await created.Database.EnsureCreatedAsync();

        foreach (var pragma in new[] { "pragma_table_info", "pragma_index_list", "pragma_foreign_key_list" })
            Assert.Equal(await SignatureAsync(created, pragma), await SignatureAsync(upgraded, pragma));
        Assert.Contains("IX_CourseTrainers_CourseId_TrainerId|1", await SignatureAsync(created, "pragma_index_list"));
        Assert.Contains("IX_CourseTrainers_TrainerId|0", await SignatureAsync(created, "pragma_index_list"));
    }

    [Fact]
    public async Task Backfill_adds_each_past_intake_trainer_once_and_keeps_removed_trainers_out()
    {
        var (connection, db) = await OpenAsync();
        await using var connectionScope = connection;
        await using var dbScope = db;
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(
            new User { Id = 1, Email = "creator@example.com", FullName = "Creator" },
            new User { Id = 2, Email = "trainer-a@example.com", FullName = "Trainer A" },
            new User { Id = 3, Email = "trainer-b@example.com", FullName = "Trainer B" });
        db.Courses.AddRange(
            new Course { Id = 10, Code = "C-10", Title = "Ten", TrainerId = 1, CreatorId = 1 },
            new Course { Id = 11, Code = "C-11", Title = "Eleven", TrainerId = 1, CreatorId = 1 });
        db.CourseIntakes.AddRange(Intake(10, 2), Intake(10, 2), Intake(10, 3), Intake(11, 2));
        await db.SaveChangesAsync();

        await CourseTrainerSchema.BackfillAsync(db);
        await CourseTrainerSchema.BackfillAsync(db);

        var roster = await db.CourseTrainers.AsNoTracking().OrderBy(x => x.CourseId).ThenBy(x => x.TrainerId).ToListAsync();
        Assert.Equal([(10, 2), (10, 3), (11, 2)], roster.Select(x => (x.CourseId, x.TrainerId)));
        Assert.All(roster, row =>
        {
            Assert.Equal(CourseTrainerStatus.Accepted, row.Status);
            Assert.Equal(1, row.InvitedByUserId);
            Assert.NotNull(row.RespondedAt);
        });

        await db.CourseTrainers.Where(x => x.CourseId == 11).ExecuteUpdateAsync(update => update
            .SetProperty(x => x.Status, CourseTrainerStatus.Removed));
        await CourseTrainerSchema.BackfillAsync(db);

        var removed = await db.CourseTrainers.AsNoTracking().SingleAsync(x => x.CourseId == 11);
        Assert.Equal(CourseTrainerStatus.Removed, removed.Status);
        Assert.Equal(3, await db.CourseTrainers.CountAsync());
    }

    private static CourseIntake Intake(int courseId, int trainerId) => new()
    {
        CourseId = courseId,
        TrainerId = trainerId,
        RegistrationOpensAt = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        RegistrationClosesAt = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc),
        StartsAt = new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        EndsAt = new DateTime(2027, 4, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static async Task<(SqliteConnection Connection, CoLearnXDbContext Db)> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        return (connection, db);
    }

    private static async Task<string> SignatureAsync(CoLearnXDbContext db, string pragma)
    {
        var sql = pragma switch
        {
            "pragma_table_info" => "SELECT name || '|' || type || '|' || \"notnull\" || '|' || IFNULL(dflt_value, '') || '|' || pk AS Value FROM pragma_table_info('CourseTrainers') ORDER BY cid",
            "pragma_index_list" => "SELECT name || '|' || \"unique\" AS Value FROM pragma_index_list('CourseTrainers') ORDER BY name",
            "pragma_foreign_key_list" => "SELECT \"table\" || '|' || \"from\" || '|' || \"to\" || '|' || on_delete AS Value FROM pragma_foreign_key_list('CourseTrainers') ORDER BY \"table\", \"from\"",
            _ => throw new ArgumentOutOfRangeException(nameof(pragma)),
        };
        var rows = await db.Database.SqlQueryRaw<string>(sql).ToListAsync();
        return string.Join("\n", rows);
    }
}
