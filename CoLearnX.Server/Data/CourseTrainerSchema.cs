using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// EnsureCreated does not add tables to a database that already exists.
public static class CourseTrainerSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var missing = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = 'CourseTrainers'").SingleAsync(ct) == 0;
            if (missing)
            {
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE CourseTrainers (
                        Id INTEGER NOT NULL CONSTRAINT PK_CourseTrainers PRIMARY KEY AUTOINCREMENT,
                        CourseId INTEGER NOT NULL,
                        TrainerId INTEGER NOT NULL,
                        Status INTEGER NOT NULL,
                        InvitedByUserId INTEGER NOT NULL,
                        InvitedAt TEXT NOT NULL,
                        RespondedAt TEXT NULL,
                        RemovedAt TEXT NULL,
                        CONSTRAINT FK_CourseTrainers_Courses_CourseId
                            FOREIGN KEY (CourseId) REFERENCES Courses (Id) ON DELETE RESTRICT,
                        CONSTRAINT FK_CourseTrainers_Users_TrainerId
                            FOREIGN KEY (TrainerId) REFERENCES Users (Id) ON DELETE RESTRICT
                    )
                    """, ct);
            }

            await db.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS IX_CourseTrainers_CourseId_TrainerId ON CourseTrainers (CourseId, TrainerId)
                """, ct);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS IX_CourseTrainers_TrainerId ON CourseTrainers (TrainerId)
                """, ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.CourseTrainers', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.CourseTrainers (
                        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CourseTrainers PRIMARY KEY,
                        CourseId int NOT NULL,
                        TrainerId int NOT NULL,
                        Status int NOT NULL,
                        InvitedByUserId int NOT NULL,
                        InvitedAt datetime2 NOT NULL,
                        RespondedAt datetime2 NULL,
                        RemovedAt datetime2 NULL,
                        CONSTRAINT FK_CourseTrainers_Courses_CourseId
                            FOREIGN KEY (CourseId) REFERENCES dbo.Courses (Id) ON DELETE NO ACTION,
                        CONSTRAINT FK_CourseTrainers_Users_TrainerId
                            FOREIGN KEY (TrainerId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION
                    );
                END;
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseTrainers_CourseId_TrainerId' AND object_id = OBJECT_ID(N'dbo.CourseTrainers'))
                    CREATE UNIQUE INDEX IX_CourseTrainers_CourseId_TrainerId ON dbo.CourseTrainers (CourseId, TrainerId);
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CourseTrainers_TrainerId' AND object_id = OBJECT_ID(N'dbo.CourseTrainers'))
                    CREATE INDEX IX_CourseTrainers_TrainerId ON dbo.CourseTrainers (TrainerId);
                """, ct);
        }
    }

    // Trainers who already ran an Intake keep teaching that Course; existing rows, including removals, are never changed.
    public static async Task BackfillAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        var taught = await db.CourseIntakes.AsNoTracking()
            .Select(intake => new { intake.CourseId, intake.TrainerId, intake.Course.CreatorId })
            .Distinct()
            .ToListAsync(ct);
        if (taught.Count == 0)
            return;

        var existing = (await db.CourseTrainers.AsNoTracking()
                .Select(row => new { row.CourseId, row.TrainerId })
                .ToListAsync(ct))
            .Select(row => (row.CourseId, row.TrainerId))
            .ToHashSet();
        var now = DateTime.UtcNow;
        var added = false;
        foreach (var pair in taught.Where(pair => !existing.Contains((pair.CourseId, pair.TrainerId))))
        {
            db.CourseTrainers.Add(new CourseTrainer
            {
                CourseId = pair.CourseId,
                TrainerId = pair.TrainerId,
                Status = CourseTrainerStatus.Accepted,
                InvitedByUserId = pair.CreatorId,
                InvitedAt = now,
                RespondedAt = now,
            });
            added = true;
        }

        if (added)
            await db.SaveChangesAsync(ct);
    }
}
