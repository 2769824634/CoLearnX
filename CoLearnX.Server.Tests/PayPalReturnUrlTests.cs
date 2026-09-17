using CoLearnX.Server.Payments;

namespace CoLearnX.Server.Tests;

public class PayPalReturnUrlTests
{
    [Theory]
    [InlineData("https://localhost:55128/member/payment")]
    [InlineData("http://localhost:55128/member/payment")]
    [InlineData("https://abcd-55128.devtunnels.ms/member/payment")]
    [InlineData("https://abcd-55128.au.devtunnels.ms/member/payment")]
    [InlineData("https://mb2mlhcf-55128.asse.devtunnels.ms/member/payment")]
    [InlineData("https://colearnx.azurewebsites.net/member/payment")]
    [InlineData("https://colearnx-prod.azurewebsites.net/member/payment")]
    public void Accepts_local_devtunnel_and_azure_payment_urls(string url)
        => Assert.Equal(url.TrimEnd('/'), PayPalReturnUrls.Normalize(url));

    [Fact]
    public void Accepts_configured_custom_host()
        => Assert.Equal(
            "https://learn.colearnx.edu.au/member/payment",
            PayPalReturnUrls.Normalize("https://learn.colearnx.edu.au/member/payment", ["learn.colearnx.edu.au"]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/member/payment")]
    [InlineData("http://abcd-55128.devtunnels.ms/member/payment")]
    [InlineData("http://colearnx.azurewebsites.net/member/payment")]
    [InlineData("https://abcd-55128.devtunnels.ms/admin/login")]
    public void Rejects_untrusted_or_missing_urls(string? url)
        => Assert.Null(PayPalReturnUrls.Normalize(url));
}
