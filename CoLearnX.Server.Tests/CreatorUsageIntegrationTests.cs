using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class CreatorUsageIntegrationTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public CreatorUsageIntegrationTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Usage_shows_only_owned_material_events_with_course_and_trainer_details()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var creator = await db.Users.SingleAsync(user => user.Email == SeedData.CreatorEmail);
        var trainer = await db.Users.SingleAsync(user => user.Email == SeedData.TrainerEmail);
        var course = await db.Courses.FirstAsync(item => item.CreatorId == creator.Id);
        var own = new LearningMaterial { CreatorId = creator.Id, Title = "Owned usage proof", FilePath = "materials/owned.pdf" };
        var foreign = new LearningMaterial { CreatorId = trainer.Id, Title = "Foreign usage proof", FilePath = "materials/foreign.pdf" };
        db.LearningMaterials.AddRange(own, foreign);
        await db.SaveChangesAsync();
        var ownTime = new DateTime(2026, 9, 17, 9, 0, 0, DateTimeKind.Utc);
        var ownLog = new MaterialUsageLog { LearningMaterialId = own.Id, CourseId = course.Id, TrainerId = trainer.Id, UsedAt = ownTime };
        var foreignLog = new MaterialUsageLog { LearningMaterialId = foreign.Id, CourseId = course.Id, TrainerId = trainer.Id, UsedAt = ownTime.AddMinutes(1) };
        db.MaterialUsageLogs.AddRange(ownLog, foreignLog);
        await db.SaveChangesAsync();

        using var client = await ApiClient.AsCreatorAsync(_factory);
        var response = await client.GetAsync("/api/materials/usage");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var records = await response.Content.ReadFromJsonAsync<JsonArray>(ApiJson.Options);
        var record = Assert.Single(records!, item => item!["id"]!.GetValue<int>() == ownLog.Id);
        Assert.Equal(own.Id, record!["materialId"]!.GetValue<int>());
        Assert.Equal("Owned usage proof", record["materialTitle"]!.GetValue<string>());
        Assert.Equal(course.Code, record["courseCode"]!.GetValue<string>());
        Assert.Equal(trainer.FullName, record["trainerName"]!.GetValue<string>());
        Assert.Equal(ownTime, record["usedAt"]!.GetValue<DateTime>());
        Assert.DoesNotContain(records!, item => item!["id"]!.GetValue<int>() == foreignLog.Id);
    }

    [Fact]
    public async Task Usage_requires_creator_role()
    {
        using var anonymous = ApiClient.Anonymous(_factory);
        using var member = await ApiClient.AsMemberAsync(_factory);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/materials/usage")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/materials/usage")).StatusCode);
    }

    [Fact]
    public async Task Usage_rejects_a_disabled_creator_even_with_an_existing_token()
    {
        using var factory = new CoLearnXApiFactory();
        using var client = await ApiClient.AsCreatorAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var creator = await db.Users.SingleAsync(user => user.Email == SeedData.CreatorEmail);
        creator.IsActive = false;
        await db.SaveChangesAsync();

        var response = await client.GetAsync("/api/materials/usage");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
