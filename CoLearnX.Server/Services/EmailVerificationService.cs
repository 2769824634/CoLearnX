using System.Security.Cryptography;
using System.Text;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoLearnX.Server.Services;

public class EmailVerificationService(
    CoLearnXDbContext db,
    IEmailVerificationMailSender mail,
    IOptions<PasswordResetOptions> options,
    IWebHostEnvironment environment,
    ILogger<EmailVerificationService> logger)
{
    public async Task RequestAsync(User user, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!Uri.TryCreate(opts.ClientBaseUrl, UriKind.Absolute, out var clientUri)
            || clientUri.Scheme != Uri.UriSchemeHttps
            || (!environment.IsDevelopment() && !environment.IsEnvironment("Testing") && clientUri.IsLoopback))
        {
            logger.LogWarning("Email verification delivery configuration is unavailable.");
            return;
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id,
            TokenHash = Hash(token),
            RequestedAt = now,
            ExpiresAt = now.AddHours(Math.Clamp(opts.EmailVerificationLifetimeHours, 1, 168)),
        });
        await db.SaveChangesAsync(ct);

        try
        {
            var link = opts.ClientBaseUrl.TrimEnd('/') + "/verify-email#token=" + token;
            await mail.SendAsync(user.Email, link, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Email verification delivery failed; verify the configured mail service.");
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
