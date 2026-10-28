using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoLearnX.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class ProfileOccupationTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;
    public ProfileOccupationTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Occupation_defaults_to_unknown_and_survives_profile_reload()
    {
        using var client = await ApiClient.AsMemberAsync(_factory);
        var initial = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var id = initial.GetProperty("id").GetInt32();
        Assert.Equal("unknown", initial.GetProperty("occupation").GetString());

        using var save = await client.PutAsJsonAsync($"/api/users/{id}", new { occupation = "  社区教育工作者  " });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("社区教育工作者", (await save.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("occupation").GetString());
        using var unrelated = await client.PutAsJsonAsync($"/api/users/{id}", new { bio = "Updated bio" });
        Assert.Equal(HttpStatusCode.OK, unrelated.StatusCode);
        Assert.Equal("社区教育工作者", (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("occupation").GetString());

        using var tooLong = await client.PutAsJsonAsync($"/api/users/{id}", new { occupation = new string('x', 121) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        using var clear = await client.PutAsJsonAsync($"/api/users/{id}", new { occupation = "  " });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        Assert.Equal("unknown", (await client.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("occupation").GetString());
    }

    [Fact]
    public async Task Existing_sqlite_profiles_are_upgraded_without_losing_data()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options;
        await using var db = new CoLearnXDbContext(options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Users (Id INTEGER PRIMARY KEY, Email TEXT NOT NULL)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Users (Id, Email) VALUES (1, 'existing@example.com')");
        await ProfileOccupationSchema.EnsureAsync(db);
        var defaultCount = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM Users WHERE Email = 'existing@example.com' AND Occupation = 'unknown'").SingleAsync();
        Assert.Equal(1, defaultCount);
        await db.Database.ExecuteSqlRawAsync("UPDATE Users SET Occupation = 'Teacher' WHERE Id = 1");
        await ProfileOccupationSchema.EnsureAsync(db);
        var retainedCount = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM Users WHERE Email = 'existing@example.com' AND Occupation = 'Teacher'").SingleAsync();
        Assert.Equal(1, retainedCount);
    }
}
