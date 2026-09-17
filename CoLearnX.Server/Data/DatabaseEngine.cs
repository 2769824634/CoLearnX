using System.Data.Common;

namespace CoLearnX.Server.Data;

public static class DatabaseEngine
{
    public static bool IsSqlServer(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (Has(builder, "Initial Catalog") || Has(builder, "Database"))
                return true;

            var dataSource = Get(builder, "Data Source") ?? Get(builder, "Server");
            if (string.IsNullOrWhiteSpace(dataSource))
                return false;

            return dataSource.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase)
                || dataSource.Contains("database.chinacloudapi.cn", StringComparison.OrdinalIgnoreCase)
                || Has(builder, "Server") && !LooksLikeSqliteFile(dataSource);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static string WithSqlServerDefaults(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (!Has(builder, "MultipleActiveResultSets"))
            builder["MultipleActiveResultSets"] = true;
        return builder.ConnectionString;
    }

    static bool LooksLikeSqliteFile(string dataSource)
        => dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            || dataSource.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            || dataSource.Contains(".db;", StringComparison.OrdinalIgnoreCase);

    static bool Has(DbConnectionStringBuilder builder, string key)
        => builder.ContainsKey(key) && builder[key] is not null && !string.IsNullOrWhiteSpace(Convert.ToString(builder[key]));

    static string? Get(DbConnectionStringBuilder builder, string key)
        => builder.ContainsKey(key) ? Convert.ToString(builder[key]) : null;
}
