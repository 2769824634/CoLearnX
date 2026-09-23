using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;

namespace CoLearnX.Server.Services;

public static class AccountMail
{
    public const string FromDisplayName = "CoLearnX";
    public const string TeamName = "CoLearnX Team";
    public const string SiteUrl = "https://colearnx.xyz";
    public const string ContactName = "Huang Yousheng";
    public const string ContactEmail = "youshengh1@colearnx.xyz";

    public sealed record Message(string Subject, string HtmlBody, string PlainTextBody);

    public static Message Verification(string link, int lifetimeHours = 24, string? contactEmail = null) =>
        Compose(
            "Verify your CoLearnX account",
            "Verify your email",
            "Thank you for creating a CoLearnX account. Please confirm this email address to finish signing in.",
            "Verify email",
            $"This one-time link expires in {lifetimeHours} hours.",
            "If you did not create this account, you can ignore this email.",
            link,
            contactEmail);

    public static Message PasswordReset(string link, int lifetimeMinutes = 30, string? contactEmail = null) =>
        Compose(
            "Reset your CoLearnX password",
            "Reset your password",
            "We received a request to reset the password for your CoLearnX account.",
            "Reset password",
            $"This one-time link expires in {lifetimeMinutes} minutes.",
            "If you did not request this, you can ignore this email. Your password will remain unchanged.",
            link,
            contactEmail);

    public static MailMessage CreateMessage(string fromAddress, string to, Message composed)
    {
        var message = new MailMessage
        {
            From = new MailAddress(fromAddress, FromDisplayName),
            Subject = composed.Subject,
            Body = composed.HtmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(to);
        message.ReplyToList.Add(new MailAddress(fromAddress, ContactName));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            composed.PlainTextBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        return message;
    }

    static Message Compose(
        string subject,
        string heading,
        string intro,
        string buttonLabel,
        string expiry,
        string ignore,
        string link,
        string? contactEmail)
    {
        contactEmail = string.IsNullOrWhiteSpace(contactEmail) ? ContactEmail : contactEmail.Trim();
        var safeContact = WebUtility.HtmlEncode(contactEmail);
        var safeLink = WebUtility.HtmlEncode(link);
        var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{WebUtility.HtmlEncode(subject)}}</title>
            </head>
            <body style="margin:0;padding:0;background:#eef1f5;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#eef1f5;padding:24px 0;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="560" cellspacing="0" cellpadding="0" style="width:560px;max-width:560px;background:#ffffff;border:1px solid #e8ecf0;">
                      <tr>
                        <td style="height:4px;background:#10d2af;font-size:0;line-height:0;">&nbsp;</td>
                      </tr>
                      <tr>
                        <td style="padding:28px 36px 8px;font-family:'Segoe UI',Arial,sans-serif;">
                          <div style="font-size:12px;letter-spacing:0.18em;text-transform:uppercase;color:#7241ff;font-weight:700;">CoLearnX</div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:8px 36px 0;font-family:'Segoe UI',Arial,sans-serif;color:#333333;">
                          <h1 style="margin:0 0 16px;font-size:22px;line-height:1.3;font-weight:600;">{{WebUtility.HtmlEncode(heading)}}</h1>
                          <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#5f6f81;">{{WebUtility.HtmlEncode(intro)}}</p>
                          <table role="presentation" cellspacing="0" cellpadding="0" border="0">
                            <tr>
                              <td align="center" bgcolor="#7241ff" style="border-radius:6px;">
                                <a href="{{safeLink}}" target="_blank" style="display:inline-block;padding:12px 24px;font-family:'Segoe UI',Arial,sans-serif;font-size:15px;color:#ffffff;text-decoration:none;font-weight:600;">{{WebUtility.HtmlEncode(buttonLabel)}}</a>
                              </td>
                            </tr>
                          </table>
                          <p style="margin:24px 0 8px;font-size:13px;line-height:1.6;color:#5f6f81;">If the button does not work, copy and paste this link into your browser:</p>
                          <p style="margin:0 0 16px;font-size:13px;line-height:1.6;word-break:break-all;">
                            <a href="{{safeLink}}" style="color:#7241ff;">{{safeLink}}</a>
                          </p>
                          <p style="margin:0 0 8px;font-size:13px;line-height:1.6;color:#5f6f81;">{{WebUtility.HtmlEncode(expiry)}}</p>
                          <p style="margin:0;font-size:13px;line-height:1.6;color:#5f6f81;">{{WebUtility.HtmlEncode(ignore)}}</p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:28px 36px 32px;font-family:'Segoe UI',Arial,sans-serif;">
                          <div style="border-top:1px solid #e8ecf0;padding-top:20px;">
                            <p style="margin:0 0 4px;font-size:14px;font-weight:600;color:#333333;">{{TeamName}}</p>
                            <p style="margin:0 0 16px;font-size:13px;">
                              <a href="{{SiteUrl}}" style="color:#7241ff;text-decoration:none;">{{SiteUrl}}</a>
                            </p>
                            <p style="margin:0;font-size:13px;line-height:1.6;color:#5f6f81;">
                              {{ContactName}}<br>
                              <a href="mailto:{{safeContact}}" style="color:#7241ff;text-decoration:none;">{{safeContact}}</a>
                            </p>
                          </div>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        var plain = $"""
            {heading}

            {intro}

            {buttonLabel}:
            {link}

            {expiry}

            {ignore}

            —
            {TeamName}
            {SiteUrl}

            {ContactName}
            {contactEmail}
            """;

        return new Message(subject, html, plain);
    }
}
