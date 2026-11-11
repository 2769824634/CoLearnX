using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

public static class CertificateFileSchema
{
    public static async Task EnsureAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        if (db.Database.IsSqlite())
        {
            var missingPath = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('UserCertificates') WHERE name = 'FilePath'")
                .SingleAsync(ct) == 0;
            if (missingPath)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserCertificates ADD COLUMN FilePath TEXT NULL", ct);
            var missingName = await db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(*) AS Value FROM pragma_table_info('UserCertificates') WHERE name = 'OriginalFileName'")
                .SingleAsync(ct) == 0;
            if (missingName)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE UserCertificates ADD COLUMN OriginalFileName TEXT NULL", ct);
        }
        else if (db.Database.IsSqlServer())
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF COL_LENGTH('dbo.UserCertificates', 'FilePath') IS NULL
                    ALTER TABLE dbo.UserCertificates ADD FilePath nvarchar(512) NULL;
                IF COL_LENGTH('dbo.UserCertificates', 'OriginalFileName') IS NULL
                    ALTER TABLE dbo.UserCertificates ADD OriginalFileName nvarchar(260) NULL;
                """, ct);
        }
    }
}
