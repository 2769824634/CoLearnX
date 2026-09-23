using System.Net.Mime;
using CoLearnX.Server.Services;

namespace CoLearnX.Server.Tests;

public class AccountMailTests
{
    [Fact]
    public void Verification_mail_is_formal_html_with_button_and_team_signature()
    {
        var link = "https://colearnx.xyz/verify-email#token=abc123";
        var mail = AccountMail.Verification(link, lifetimeHours: 24);

        Assert.Equal("Verify your CoLearnX account", mail.Subject);
        Assert.Contains("<!DOCTYPE html>", mail.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Verify email", mail.HtmlBody);
        Assert.Contains($"href=\"{link}\"", mail.HtmlBody);
        Assert.Contains(link, mail.PlainTextBody);
        Assert.Contains("24 hours", mail.HtmlBody);
        Assert.Contains("24 hours", mail.PlainTextBody);
        Assert.Contains("CoLearnX Team", mail.HtmlBody);
        Assert.Contains("CoLearnX Team", mail.PlainTextBody);
        Assert.Contains("https://colearnx.xyz", mail.HtmlBody);
        Assert.Contains("Huang Yousheng", mail.HtmlBody);
        Assert.Contains("Huang Yousheng", mail.PlainTextBody);
        Assert.Contains("youshengh1@colearnx.xyz", mail.HtmlBody);
        Assert.Contains("youshengh1@colearnx.xyz", mail.PlainTextBody);
        Assert.DoesNotContain("Open this one-time link to verify your email:", mail.PlainTextBody);
    }

    [Fact]
    public void Password_reset_mail_is_formal_html_with_button_and_team_signature()
    {
        var link = "https://colearnx.xyz/reset-password#token=xyz789";
        var mail = AccountMail.PasswordReset(link, lifetimeMinutes: 30);

        Assert.Equal("Reset your CoLearnX password", mail.Subject);
        Assert.Contains("<!DOCTYPE html>", mail.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reset password", mail.HtmlBody);
        Assert.Contains($"href=\"{link}\"", mail.HtmlBody);
        Assert.Contains(link, mail.PlainTextBody);
        Assert.Contains("30 minutes", mail.HtmlBody);
        Assert.Contains("30 minutes", mail.PlainTextBody);
        Assert.Contains("CoLearnX Team", mail.HtmlBody);
        Assert.Contains("Huang Yousheng", mail.PlainTextBody);
        Assert.Contains("youshengh1@colearnx.xyz", mail.HtmlBody);
        Assert.DoesNotContain("Open this one-time link to reset your password:", mail.PlainTextBody);
    }

    [Fact]
    public void Html_encodes_the_action_link()
    {
        var link = "https://colearnx.xyz/verify-email?next=a&b=2#token=\"quoted\"";
        var mail = AccountMail.Verification(link);

        Assert.DoesNotContain(link, mail.HtmlBody);
        Assert.Contains("&amp;", mail.HtmlBody);
        Assert.Contains("&quot;", mail.HtmlBody);
        Assert.Contains(link, mail.PlainTextBody);
    }

    [Fact]
    public void Mail_message_uses_display_name_html_body_and_plain_text_fallback()
    {
        var composed = AccountMail.Verification("https://colearnx.xyz/verify-email#token=abc");
        using var message = AccountMail.CreateMessage("youshengh1@colearnx.xyz", "member@example.com", composed);

        Assert.Equal("CoLearnX", message.From!.DisplayName);
        Assert.Equal("youshengh1@colearnx.xyz", message.From.Address);
        Assert.Equal("member@example.com", message.To.Single().Address);
        Assert.Equal(composed.Subject, message.Subject);
        Assert.True(message.IsBodyHtml);
        Assert.Contains("Verify email", message.Body);
        Assert.Contains(MediaTypeNames.Text.Plain, message.AlternateViews.Select(v => v.ContentType.MediaType));
        Assert.Equal("youshengh1@colearnx.xyz", message.ReplyToList.Single().Address);
        Assert.Equal("Huang Yousheng", message.ReplyToList.Single().DisplayName);
    }
}
