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
    public void Accepts_local_and_devtunnel_payment_urls(string url)
        => Assert.Equal(url.TrimEnd('/'), PayPalReturnUrls.Normalize(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/member/payment")]
    [InlineData("http://abcd-55128.devtunnels.ms/member/payment")]
    [InlineData("https://abcd-55128.devtunnels.ms/admin/login")]
    public void Rejects_untrusted_or_missing_urls(string? url)
        => Assert.Null(PayPalReturnUrls.Normalize(url));
}
