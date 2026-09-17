using System.Net;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;

namespace CoLearnX.Server.Tests;

public class AvatarUploadTests : IClassFixture<CoLearnXApiFactory>
{
    private readonly CoLearnXApiFactory _factory;

    public AvatarUploadTests(CoLearnXApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Member_can_upload_and_read_own_avatar_but_another_member_cannot()
    {
        using var owner = await ApiClient.AsMemberAsync(_factory);
        var me = await owner.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        using var form = new MultipartFormDataContent();
        var bytes = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 0 };
        form.Add(new ByteArrayContent(bytes), "file", "portrait.png");

        var uploaded = await owner.PostAsync($"/api/users/{me!.Id}/avatar", form);

        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        var profile = await uploaded.Content.ReadFromJsonAsync<UserMeDto>(ApiJson.Options);
        Assert.NotNull(profile?.AvatarUrl);
        var image = await owner.GetAsync(profile.AvatarUrl);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(bytes, await image.Content.ReadAsByteArrayAsync());
        using var other = await ApiClient.AsCreatorAsync(_factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(profile.AvatarUrl)).StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_non_image_content()
    {
        using var owner = await ApiClient.AsMemberAsync(_factory);
        var me = await owner.GetFromJsonAsync<UserMeDto>("/api/auth/me", ApiJson.Options);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("hello"u8.ToArray()), "file", "portrait.png");

        var response = await owner.PostAsync($"/api/users/{me!.Id}/avatar", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
