using System.Security.Cryptography;
using System.Text;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoLearnX.Server.Services;

public class PasswordResetService(
    CoLearnXDbContext db, IPasswordResetMailSender mail, IOptions<PasswordResetOptions> options,
    IWebHostEnvironment environment, ILogger<PasswordResetService> logger)
{
    public const string RequestMessage = "If the account is eligible, a password reset link will be sent.";

    public async Task RequestAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Email == normalized && x.IsActive, ct);
        if (user is null) return; // AdminAccount is intentionally a different identity store.
        var opts = options.Value;
        if (!Uri.TryCreate(opts.ClientBaseUrl, UriKind.Absolute, out var clientUri)
            || clientUri.Scheme != Uri.UriSchemeHttps
            || (!environment.IsDevelopment() && !environment.IsEnvironment("Testing") && clientUri.IsLoopback))
        {
            logger.LogWarning("Password reset delivery configuration is unavailable.");
            return;
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var hash = Hash(token);
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-Math.Clamp(opts.CooldownSeconds, 60, 3600));
        var expires = now.AddMinutes(Math.Clamp(opts.LifetimeMinutes, 1, 60));
        // Conditional update is the persisted per-account rate gate, including concurrent requests.
        var changed = await db.PasswordResetTokens.Where(x => x.UserId == user.Id && x.RequestedAt <= cutoff)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TokenHash, hash)
                .SetProperty(x => x.RequestedAt, now).SetProperty(x => x.ExpiresAt, expires)
                .SetProperty(x => x.UsedAt, (DateTime?)null), ct);
        if (changed == 0)
        {
            if (await db.PasswordResetTokens.AnyAsync(x => x.UserId == user.Id, ct)) return;
            var row = new PasswordResetToken { UserId = user.Id, TokenHash = hash, RequestedAt = now, ExpiresAt = expires };
            db.PasswordResetTokens.Add(row);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(row).State = EntityState.Detached;
                if (await db.PasswordResetTokens.AnyAsync(x => x.UserId == user.Id, ct)) return;
                throw;
            }
        }
        try
        {
            await mail.SendAsync(user.Email, opts.ClientBaseUrl.TrimEnd('/') + "/reset-password#token=" + token, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Do not log recipient, link, credential, or SMTP exception text.
            logger.LogWarning("Password reset delivery failed; verify the configured mail service.");
        }
    }

    public async Task<bool> ResetAsync(string token, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 64 || !token.All(Uri.IsHexDigit)) return false;
        var hash = Hash(token);
        var now = DateTime.UtcNow;
        var candidate = await db.PasswordResetTokens.AsNoTracking()
            .Where(x => x.TokenHash == hash && x.UsedAt == null && x.ExpiresAt > now && x.User.IsActive)
            .Select(x => new { x.UserId, x.User.Email }).SingleOrDefaultAsync(ct);
        if (candidate is null) return false;
        if (!PasswordRules.Meets(newPassword, candidate.Email)) throw new FormatException(PasswordRules.Hint);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        // First write atomically claims the token. Password and session invalidation share its transaction.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var claimed = await db.PasswordResetTokens
                .Where(x => x.UserId == candidate.UserId && x.TokenHash == hash && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedAt, (DateTime?)DateTime.UtcNow), ct);
            if (claimed != 1) return false;
            var updated = await db.Users.Where(x => x.Id == candidate.UserId && x.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PasswordHash, passwordHash)
                    .SetProperty(x => x.SessionStamp, Guid.NewGuid()), ct);
            if (updated != 1) return false;
            await transaction.CommitAsync(ct);
            return true;
        });
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
