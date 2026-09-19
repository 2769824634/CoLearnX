using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class NotificationApiTests
{
    [Fact]
    public async Task Revoked_members_cannot_read_or_mutate_notifications_with_old_tokens()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        await db.UserRoles.Where(r => r.User.Email == SeedData.MemberEmail).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/notifications/my")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsync("/api/notifications/1/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PutAsync("/api/notifications/read-all", null)).StatusCode);
    }

    [Fact]
    public async Task Member_notifications_are_private_persistent_idempotent_and_use_controlled_links()
    {
        using var factory = new CoLearnXApiFactory();
        using var member = await ApiClient.AsMemberAsync(factory);
        var me = (await member.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options))!;
        int ownId, foreignId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var trainerId = await db.Users.Where(u => u.Email == SeedData.TrainerEmail).Select(u => u.Id).SingleAsync();
            var own = new Notification { UserId = me.Id, Code = "CertificateSubmitted", Title = "Submitted", Body = "Awaiting Trainer review." };
            var other = new Notification { UserId = trainerId, Code = "N-01", Title = "Private", Body = "Another account" };
            db.Notifications.AddRange(own, other,
                new Notification { UserId = me.Id, Code = "https://example.com", Title = "Unknown event", Body = "No redirect" },
                new Notification { UserId = me.Id, Code = "N-topup", Title = "Credits", Body = "Payment confirmed" });
            await db.SaveChangesAsync();
            ownId = own.Id;
            foreignId = other.Id;
        }

        var response = await member.GetAsync("/api/notifications/my");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var before = (await response.Content.ReadFromJsonAsync<JsonElement>());
        var items = before.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, n => n.GetProperty("id").GetInt32() == foreignId);
        Assert.Equal("/member/badges", items.Single(n => n.GetProperty("id").GetInt32() == ownId).GetProperty("targetPath").GetString());
        Assert.Equal(JsonValueKind.Null, items.Single(n => n.GetProperty("title").GetString() == "Unknown event").GetProperty("targetPath").ValueKind);
        Assert.Equal("/member/payment", items.Single(n => n.GetProperty("title").GetString() == "Credits").GetProperty("targetPath").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await member.PutAsync($"/api/notifications/{foreignId}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await member.PutAsync($"/api/notifications/{ownId}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await member.PutAsync($"/api/notifications/{ownId}/read", null)).StatusCode);
        var after = await member.GetFromJsonAsync<JsonElement>("/api/notifications/my");
        Assert.Equal(before.GetProperty("unreadCount").GetInt32() - 1, after.GetProperty("unreadCount").GetInt32());
        Assert.True(after.GetProperty("items").EnumerateArray().Single(n => n.GetProperty("id").GetInt32() == ownId).GetProperty("isRead").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await member.PutAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await member.PutAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await member.GetFromJsonAsync<JsonElement>("/api/notifications/my")).GetProperty("unreadCount").GetInt32());
        using var verify = factory.Services.CreateScope();
        Assert.False((await verify.ServiceProvider.GetRequiredService<CoLearnXDbContext>().Notifications.SingleAsync(n => n.Id == foreignId)).IsRead);
    }

    [Fact]
    public async Task Notification_endpoints_reject_anonymous_and_other_roles()
    {
        using var factory = new CoLearnXApiFactory();
        using var anonymous = ApiClient.Anonymous(factory);
        using var trainer = await ApiClient.AsRoleAsync(factory, SeedData.TrainerEmail, "Trainer");
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications/my")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await trainer.GetAsync("/api/notifications/my")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/notifications/my")).StatusCode);
    }
}
