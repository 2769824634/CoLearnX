using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class DatabaseEngineTests
{
    [Theory]
    [InlineData("Data Source=colearnx-later-v1.db", false)]
    [InlineData("Data Source=:memory:", false)]
    [InlineData("Server=tcp:colearnx.database.windows.net,1433;Initial Catalog=colearnx;User ID=colearnx;Password=secret;Encrypt=True;", true)]
    [InlineData("Server=localhost;Database=CoLearnX;Trusted_Connection=True;TrustServerCertificate=True", true)]
    public void Detects_sql_server_connection_strings(string connectionString, bool sqlServer)
        => Assert.Equal(sqlServer, DatabaseEngine.IsSqlServer(connectionString));

    [Fact]
    public void SqlServer_model_builds()
    {
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>()
            .UseSqlServer("Server=tcp:example.database.windows.net,1433;Initial Catalog=colearnx;User ID=x;Password=y;Encrypt=True")
            .Options;
        using var db = new CoLearnXDbContext(options);
        Assert.NotNull(db.Model.FindEntityType(typeof(AdminAccount)));
        Assert.NotNull(db.Model.FindEntityType(typeof(CourseIntakeApplication)));
    }
}
