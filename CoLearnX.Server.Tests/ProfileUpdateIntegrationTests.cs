using System.Net;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class ProfileUpdateIntegrationTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public ProfileUpdateIntegrationTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Multi_role_profile_survives_hiding_and_role_text_can_be_cleared()
    {
        using var client = await ApiClient.AsRoleAsync(_factory, SeedData.TrainerEmail, "Trainer");
        var me = await client.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.NotNull(me);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            db.UserRoles.Add(new UserRole { UserId = me.Id, Role = AppRole.Creator, IsVisible = true });
            await db.SaveChangesAsync();
        }

        var profile = new UpdateProfileRequest("Xiao Ming", "Ming", "0412345678", "Creative teacher",
            null, null, null, new Dictionary<string, bool> { ["member"] = true, ["trainer"] = true, ["creator"] = false },
            "Workshops", "Trainer", "Design", "Graphic Designer");
        using var save = await client.PutAsJsonAsync($"/api/users/{me.Id}", profile, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var updated = await save.Content.ReadFromJsonAsync<UserMeDto>(ApiJson.Options);
        Assert.NotNull(updated);
        Assert.Equal("Xiao Ming", updated.FullName);
        Assert.Equal("Ming", updated.DisplayName);
        Assert.Equal("0412345678", updated.Phone);
        Assert.Equal("Creative teacher", updated.Bio);
        Assert.Equal("Trainer", updated.TrainerHeadline);
        Assert.Equal("Workshops", updated.Specialisations);
        Assert.Equal("Graphic Designer", updated.CreatorHeadline);
        Assert.Equal("Design", updated.ExpertiseTags);
        Assert.False(updated.IdentityVisibility["creator"]);

        var reloaded = await client.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.NotNull(reloaded);
        Assert.Contains("Creator", reloaded.Roles);
        Assert.False(reloaded.IdentityVisibility["creator"]);
        Assert.Equal("Graphic Designer", reloaded.CreatorHeadline);
        Assert.Equal("Design", reloaded.ExpertiseTags);
        Assert.Equal("Trainer", reloaded.TrainerHeadline);

        using var clear = await client.PutAsJsonAsync($"/api/users/{me.Id}", profile with
        {
            CreatorHeadline = "", ExpertiseTags = "",
            IdentityVisibility = new Dictionary<string, bool> { ["creator"] = true }
        }, ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        reloaded = await client.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.IdentityVisibility["creator"]);
        Assert.Equal("", reloaded.CreatorHeadline);
        Assert.Equal("", reloaded.ExpertiseTags);
        Assert.Equal("Trainer", reloaded.TrainerHeadline);
    }

    [Fact]
    public async Task Member_cannot_create_ungranted_role_profiles_by_updating_profile()
    {
        using var client = await ApiClient.AsMemberAsync(_factory);
        var me = await client.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.NotNull(me);
        using var save = await client.PutAsJsonAsync($"/api/users/{me.Id}", new UpdateProfileRequest(
            null, null, null, null, null, null, null,
            new Dictionary<string, bool> { ["trainer"] = true, ["creator"] = true },
            "Teaching", "Trainer", "Design", "Creator"), ApiJson.Options);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var updated = await client.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        Assert.NotNull(updated);
        Assert.DoesNotContain("Trainer", updated.Roles);
        Assert.DoesNotContain("Creator", updated.Roles);
        Assert.Null(updated.TrainerHeadline);
        Assert.Null(updated.Specialisations);
        Assert.Null(updated.CreatorHeadline);
        Assert.Null(updated.ExpertiseTags);
        Assert.False(updated.IdentityVisibility.ContainsKey("trainer"));
        Assert.False(updated.IdentityVisibility.ContainsKey("creator"));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        Assert.False(await db.TrainerProfiles.AnyAsync(p => p.UserId == me.Id));
        Assert.False(await db.CreatorProfiles.AnyAsync(p => p.UserId == me.Id));
    }
}
