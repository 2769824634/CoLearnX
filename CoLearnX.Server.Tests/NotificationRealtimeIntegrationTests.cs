using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace CoLearnX.Server.Tests;

public class NotificationRealtimeIntegrationTests
{
    [Fact]
    public async Task Approve_PushesTheDecisionOnlyToTheCourseCreator()
    {
        await using var factory = new CoLearnXApiFactory();
        using var creator = await LoginAsync(factory, SeedData.CreatorEmail, "Creator");
        using var trainer = await LoginAsync(factory, SeedData.TrainerEmail, "Trainer");
        await using var creatorConnection = Connect(factory, Token(creator));
        await using var trainerConnection = Connect(factory, Token(trainer));
        var creatorReceived = new TaskCompletionSource<NotificationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var trainerReceived = new TaskCompletionSource<NotificationDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        creatorConnection.On<NotificationDto>("notification", notification => creatorReceived.TrySetResult(notification));
        trainerConnection.On<NotificationDto>("notification", notification => trainerReceived.TrySetResult(notification));
        await creatorConnection.StartAsync();
        await trainerConnection.StartAsync();

        using var admin = await LoginAdminAsync(factory);
        var pending = await admin.GetFromJsonAsync<List<AdminCourseDto>>("/api/admin/courses?status=PendingApproval");
        var course = Assert.Single(pending!);
        var review = await admin.PostAsJsonAsync($"/api/admin/courses/{course.Id}/review", new AdminReviewRequest("Approve", null));
        review.EnsureSuccessStatusCode();

        var winner = await Task.WhenAny(creatorReceived.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(creatorReceived.Task, winner);
        var notification = await creatorReceived.Task;
        Assert.Equal("N-course-published", notification.Type);
        Assert.Equal("/creator/approvals", notification.TargetPath);
        Assert.Contains(course.Code, notification.Message);
        Assert.False(notification.IsRead);
        await Task.Delay(300);
        Assert.False(trainerReceived.Task.IsCompleted);
    }

    [Fact]
    public async Task Hub_RejectsAConnectionWithoutAUserToken()
    {
        await using var factory = new CoLearnXApiFactory();
        await using var connection = Connect(factory, null);
        var error = await Assert.ThrowsAnyAsync<HttpRequestException>(() => connection.StartAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    private static HubConnection Connect(CoLearnXApiFactory factory, string? token)
        => new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    private static async Task<HttpClient> LoginAsync(CoLearnXApiFactory factory, string email, string role)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, SeedData.DemoPassword, role));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static async Task<HttpClient> LoginAdminAsync(CoLearnXApiFactory factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/admin/auth/login", new AdminLoginRequest(SeedData.AdminEmail, SeedData.DemoPassword));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<AdminAuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static string Token(HttpClient client)
        => client.DefaultRequestHeaders.Authorization!.Parameter!;
}
