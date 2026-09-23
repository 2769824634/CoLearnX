using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Http;

namespace CoLearnX.Server.Tests;

public class PasswordResetLinksTests
{
    [Fact]
    public void Production_replaces_localhost_config_with_the_https_request_host()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("colearnx.azurewebsites.net");

        var origin = PasswordResetLinks.ResolveOrigin("https://localhost:55128", allowLoopback: false, request);

        Assert.Equal("https://colearnx.azurewebsites.net", origin);
    }

    [Fact]
    public void Production_uses_https_request_host_when_config_is_missing()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "https";
        request.Host = new HostString("colearnx.azurewebsites.net");

        Assert.Equal("https://colearnx.azurewebsites.net", PasswordResetLinks.ResolveOrigin(null, allowLoopback: false, request));
    }

    [Fact]
    public void Production_uses_website_hostname_when_there_is_no_request()
    {
        var origin = PasswordResetLinks.ResolveOrigin(
            "https://localhost:55128",
            allowLoopback: false,
            request: null,
            websiteHostName: "colearnx.azurewebsites.net");

        Assert.Equal("https://colearnx.azurewebsites.net", origin);
    }

    [Fact]
    public void Development_keeps_a_configured_localhost_origin()
    {
        var origin = PasswordResetLinks.ResolveOrigin("https://localhost:55128", allowLoopback: true, request: null);
        Assert.Equal("https://localhost:55128", origin);
    }

    [Fact]
    public void Production_rejects_http_and_loopback_without_a_public_https_host()
    {
        var request = new DefaultHttpContext().Request;
        request.Scheme = "http";
        request.Host = new HostString("localhost:5088");

        Assert.Null(PasswordResetLinks.ResolveOrigin("https://localhost:55128", allowLoopback: false, request));
    }
}
