using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoLearnX.Server.Services;

public class EmailVerificationService(
    CoLearnXDbContext db,
    IEmailVerificationMailSender mail,
    IOptions<PasswordResetOptions> options,
    IWebHostEnvironment environment,
    IHttpContextAccessor http,
    ILogger<EmailVerificationService> logger)
{
    public const string ResendMessage = "If the account is eligible, a verification link will be sent.";

    public async Task RequestByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(
            x => x.Email == normalized && x.IsActive && x.EmailVerifiedAt == null, ct);
        if (user is null) return;
        await RequestAsync(user, ct);
    }

    public async Task RequestAsync(User user, CancellationToken ct = default)
    {
        if (user.EmailVerifiedAt is not null) return;
        var opts = options.Value;
        var allowLoopback = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var origin = PasswordResetLinks.ResolveOrigin(
            opts.ClientBaseUrl,
            allowLoopback,
            http.HttpContext?.Request,
            Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") is { Length: > 0 }
                ? Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME")
                : null);
        if (string.IsNullOrWhiteSpace(origin))
        {
            logger.LogWarning("Email verification delivery configuration is unavailable.");
            return;
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(token);
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-Math.Clamp(opts.CooldownSeconds, 60, 3600));
        var expires = now.AddHours(Math.Clamp(opts.EmailVerificationLifetimeHours, 1, 168));
        var changed = await db.EmailVerificationTokens.Where(x => x.UserId == user.Id && x.RequestedAt <= cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TokenHash, hash)
                .SetProperty(x => x.RequestedAt, now)
                .SetProperty(x => x.ExpiresAt, expires), ct);
        if (changed == 0)
        {
            if (await db.EmailVerificationTokens.AnyAsync(x => x.UserId == user.Id, ct)) return;
            var row = new EmailVerificationToken
            {
                UserId = user.Id,
                TokenHash = hash,
                RequestedAt = now,
                ExpiresAt = expires,
            };
            db.EmailVerificationTokens.Add(row);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(row).State = EntityState.Detached;
                if (await db.EmailVerificationTokens.AnyAsync(x => x.UserId == user.Id, ct)) return;
                throw;
            }
        }

        try
        {
            var link = origin.TrimEnd('/') + "/verify-email#token=" + token;
            await mail.SendAsync(user.Email, link, ct);
        }
        catch (SmtpException ex)
        {
            logger.LogWarning("Email verification delivery failed; smtpStatus={Status}", ex.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Email verification delivery failed; error={Error}", ex.GetType().Name);
        }
    }

    public async Task<bool> VerifyAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 64 || !token.All(Uri.IsHexDigit)) return false;
        var hash = Hash(token);
        var candidate = await db.EmailVerificationTokens.AsNoTracking()
            .Where(x => x.TokenHash == hash && x.ExpiresAt > DateTime.UtcNow && x.User.IsActive && x.User.EmailVerifiedAt == null)
            .Select(x => x.UserId)
            .SingleOrDefaultAsync(ct);
        if (candidate == 0) return false;

        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var claimed = await db.EmailVerificationTokens
                .Where(x => x.UserId == candidate && x.TokenHash == hash && x.ExpiresAt > DateTime.UtcNow)
                .ExecuteDeleteAsync(ct);
            if (claimed != 1) return false;
            var updated = await db.Users.Where(x => x.Id == candidate && x.IsActive && x.EmailVerifiedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.EmailVerifiedAt, DateTime.UtcNow), ct);
            if (updated != 1) return false;
            await transaction.CommitAsync(ct);
            return true;
        });
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
