using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using CoLearnX.Server.Data;

namespace CoLearnX.Server.Tests;

public class ProfileCountryTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;
    public ProfileCountryTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Country_is_saved_and_returned_after_reloading_profile()
    {
        using var client = await ApiClient.AsMemberAsync(_factory);
        var initial = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var id = initial.GetProperty("id").GetInt32();
        using var response = await client.PutAsJsonAsync($"/api/users/{id}", new { country = "Hong Kong (China)" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Hong Kong (China)", updated.GetProperty("country").GetString());
        var reloaded = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Hong Kong (China)", reloaded.GetProperty("country").GetString());
        using var unrelated = await client.PutAsJsonAsync($"/api/users/{id}", new { bio = "Updated bio" });
        Assert.Equal(HttpStatusCode.OK, unrelated.StatusCode);
        var retained = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Hong Kong (China)", retained.GetProperty("country").GetString());
        using var tooLong = await client.PutAsJsonAsync($"/api/users/{id}", new { country = new string('x', 121) });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        using var clear = await client.PutAsJsonAsync($"/api/users/{id}", new { country = "" });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var cleared = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("", cleared.GetProperty("country").GetString());
    }
    [Fact]
    public async Task Existing_sqlite_users_table_is_upgraded_idempotently_without_losing_data()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options;
        await using var db = new CoLearnXDbContext(options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Users (Id INTEGER PRIMARY KEY, Email TEXT NOT NULL)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO Users (Id, Email) VALUES (1, 'existing@example.com')");
        await ProfileCountrySchema.EnsureAsync(db);
        await ProfileCountrySchema.EnsureAsync(db);
        await db.Database.ExecuteSqlRawAsync("UPDATE Users SET Country = 'Singapore' WHERE Id = 1");
        var count = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Users WHERE Email = 'existing@example.com' AND Country = 'Singapore'").SingleAsync();
        Assert.Equal(1, count);
    }
}
