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
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = "";
    public string SmtpPassword { get; set; } = "";
    public string FromAddress { get; set; } = "noreply@colearnx.test";
}

public interface IPasswordResetMailSender
{
    Task SendAsync(string email, string resetLink, CancellationToken ct);
}

public class PasswordResetMailSender(IOptions<PasswordResetOptions> options, IWebHostEnvironment env)
    : IPasswordResetMailSender
{
    public async Task SendAsync(string email, string resetLink, CancellationToken ct)
    {
        var opts = options.Value;
        using var message = new MailMessage(opts.FromAddress, email)
        {
            Subject = "Reset your CoLearnX password",
            Body = $"Open this one-time link to reset your password:\n\n{resetLink}\n\nIf you did not request this, ignore this email."
        };
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
                throw new InvalidOperationException("Production password-reset mail is not configured.");
            smtp.Host = opts.SmtpHost;
            smtp.Port = opts.SmtpPort;
            smtp.EnableSsl = true;
            smtp.Credentials = new NetworkCredential(opts.SmtpUsername, opts.SmtpPassword);
        }
        await smtp.SendMailAsync(message, ct);
    }
}
