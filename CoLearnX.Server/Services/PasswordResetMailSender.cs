using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CoLearnX.Server.Services;

public class PasswordResetOptions
{
    // Auto captures local mail; Smtp explicitly enables real delivery in Development.
    public string DeliveryMode { get; set; } = "Auto";
    public string ClientBaseUrl { get; set; } = "https://localhost:55128";
    public int LifetimeMinutes { get; set; } = 30;
    public int CooldownSeconds { get; set; } = 60;
    public int EmailVerificationLifetimeHours { get; set; } = 24;
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@colearnx.test";

    public static bool CanDeliver(PasswordResetOptions opts, IHostEnvironment env) =>
        env.IsEnvironment("Testing")
        || (env.IsDevelopment() && opts.DeliveryMode != "Smtp")
        || (!string.IsNullOrWhiteSpace(opts.SmtpHost)
            && !string.IsNullOrWhiteSpace(opts.SmtpUsername)
            && !string.IsNullOrWhiteSpace(opts.SmtpPassword)
            && !string.IsNullOrWhiteSpace(opts.FromAddress)
            && !opts.FromAddress.EndsWith(".test", StringComparison.OrdinalIgnoreCase));
}

public interface IPasswordResetMailSender
{
    Task SendAsync(string email, string resetLink, CancellationToken ct);
}

public interface IEmailVerificationMailSender
{
    Task SendAsync(string email, string verificationLink, CancellationToken ct);
}

public class PasswordResetMailSender(IOptions<PasswordResetOptions> options, IWebHostEnvironment env)
    : IPasswordResetMailSender, IEmailVerificationMailSender, IBusinessNotificationMailSender
{
    public bool IsAvailable => PasswordResetOptions.CanDeliver(options.Value, env) && ClientOrigin() != null;

    private Uri? ClientOrigin() => Uri.TryCreate(options.Value.ClientBaseUrl, UriKind.Absolute, out var origin)
        && (origin.Scheme == Uri.UriSchemeHttps || origin.Scheme == Uri.UriSchemeHttp)
        && string.IsNullOrEmpty(origin.UserInfo) ? origin : null;

    Task IBusinessNotificationMailSender.SendAsync(string email, string title, string body, string targetPath, CancellationToken ct)
    {
        var origin = ClientOrigin() ?? throw new InvalidOperationException("Business mail client URL is not configured.");
        var link = new Uri(origin, targetPath).AbsoluteUri;
        return SendMessageAsync(email, AccountMail.BusinessNotification(title, body, link, options.Value.FromAddress), ct);
    }

    public async Task SendAsync(string email, string resetLink, CancellationToken ct)
    {
        var composed = AccountMail.PasswordReset(resetLink, options.Value.LifetimeMinutes, options.Value.FromAddress);
        await SendMessageAsync(email, composed, ct);
    }

    async Task IEmailVerificationMailSender.SendAsync(string email, string verificationLink, CancellationToken ct)
    {
        var composed = AccountMail.Verification(verificationLink, options.Value.EmailVerificationLifetimeHours, options.Value.FromAddress);
        await SendMessageAsync(email, composed, ct);
    }

    private async Task SendMessageAsync(string email, AccountMail.Message composed, CancellationToken ct)
    {
        var opts = options.Value;
        using var message = AccountMail.CreateMessage(opts.FromAddress, email, composed);
        using var smtp = new SmtpClient();
        if (env.IsEnvironment("Testing") || (env.IsDevelopment() && opts.DeliveryMode != "Smtp"))
        {
            // Automated tests always capture mail; local SMTP must be explicitly configured.
            var directory = Path.Combine(env.ContentRootPath, "App_Data", "mail");
            Directory.CreateDirectory(directory);
            smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            smtp.PickupDirectoryLocation = directory;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(opts.SmtpHost) || opts.FromAddress.EndsWith(".test", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Production account mail is not configured.");
            smtp.Host = opts.SmtpHost;
            smtp.Port = opts.SmtpPort;
            smtp.EnableSsl = true;
            smtp.Credentials = new NetworkCredential(opts.SmtpUsername, opts.SmtpPassword);
        }
        await smtp.SendMailAsync(message, ct);
    }
}
