using System.Net;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;

namespace CoLearnX.Server.Tests;

public class WishlistApiTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public WishlistApiTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Wishlist_add_without_token_returns_unauthorized()
    {
        var client = ApiClient.Anonymous(_factory);
        var catalog = await client.GetFromJsonAsync<List<CourseListItemDto>>("/api/courses", ApiJson.Options)
            ?? [];

        var response = await client.PostAsync($"/api/courses/{catalog[0].Id}/wishlist", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wishlist_add_then_catalog_marks_inWishlist()
    {
        var member = await ApiClient.AsMemberAsync(_factory);
        var catalog = await member.GetFromJsonAsync<List<CourseListItemDto>>("/api/courses", ApiJson.Options)
            ?? [];
        var target = catalog.First(c => !c.InWishlist);

        var response = await member.PostAsync($"/api/courses/{target.Id}/wishlist", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WishlistResultDto>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.Equal(target.Id, body.CourseId);
        Assert.True(body.InWishlist);

        var detail = await member.GetFromJsonAsync<CourseDetailDto>($"/api/courses/{target.Id}", ApiJson.Options);
        Assert.True(detail!.InWishlist);
        var refreshed = await member.GetFromJsonAsync<List<CourseListItemDto>>("/api/courses", ApiJson.Options);
        Assert.True(refreshed!.Single(c => c.Id == target.Id).InWishlist);
    }

    [Fact]
    public async Task Wishlist_remove_clears_inWishlist()
    {
        var member = await ApiClient.AsMemberAsync(_factory);
        var catalog = await member.GetFromJsonAsync<List<CourseListItemDto>>("/api/courses", ApiJson.Options)
            ?? [];
        var target = catalog.First(c => c.InWishlist);

        var response = await member.DeleteAsync($"/api/courses/{target.Id}/wishlist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WishlistResultDto>(ApiJson.Options);
        Assert.NotNull(body);
        Assert.False(body.InWishlist);

        var refreshed = await member.GetFromJsonAsync<List<CourseListItemDto>>("/api/courses", ApiJson.Options);
        Assert.False(refreshed!.Single(c => c.Id == target.Id).InWishlist);
    }

    [Fact]
    public async Task Wishlist_add_unknown_course_returns_NOT_FOUND()
    {
        var member = await ApiClient.AsMemberAsync(_factory);

        var response = await member.PostAsync("/api/courses/999999/wishlist", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await ApiClient.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal("NOT_FOUND", error.Code);
    }
}
