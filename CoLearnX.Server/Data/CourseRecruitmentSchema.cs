using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class CourseRecruitmentSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var exists = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('Courses') WHERE name = 'OpenToTrainerApplications'").SingleAsync(ct);
            if (exists == 0)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Courses ADD COLUMN OpenToTrainerApplications INTEGER NOT NULL DEFAULT 1", ct);
        }
        else if (db.Database.IsSqlServer())
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH(N'dbo.Courses', N'OpenToTrainerApplications') IS NULL
                    ALTER TABLE dbo.Courses ADD OpenToTrainerApplications bit NOT NULL CONSTRAINT DF_Courses_OpenToTrainerApplications DEFAULT 1;
                """, ct);
    }
}
