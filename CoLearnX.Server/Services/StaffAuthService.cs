using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IStaffAuthService
{
    Task<StaffAuthResponse> LoginAsync(StaffLoginRequest request, CancellationToken ct = default);
    Task<StaffAccountDto> GetMeAsync(int staffAccountId, CancellationToken ct = default);
}

public sealed class StaffAuthService(
    CoLearnXDbContext db,
    IStaffTokenService tokens) : IStaffAuthService
{
    public async Task<StaffAuthResponse> LoginAsync(StaffLoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var account = await db.StaffAccounts.Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Email == email, ct);
        if (account is null || !account.IsActive || !account.User.IsActive
            || !BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        account.SessionStamp = SessionStampValidator.NewStamp();
        account.User.SessionStamp = SessionStampValidator.NewStamp();
        account.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            StaffAccountId = account.Id,
            Action = "StaffLogin",
            EntityType = nameof(StaffAccount),
            EntityId = account.Id.ToString(),
            Result = "Succeeded",
        });
        await db.SaveChangesAsync(ct);
        var (token, expiresAt) = tokens.CreateToken(account);
        return new StaffAuthResponse(token, expiresAt, Map(account));
    }

    public async Task<StaffAccountDto> GetMeAsync(int staffAccountId, CancellationToken ct = default)
    {
        var account = await db.StaffAccounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == staffAccountId && item.IsActive, ct)
            ?? throw new UnauthorizedAccessException("Staff account is unavailable.");
        return Map(account);
    }

    private static StaffAccountDto Map(StaffAccount account) => new(
        account.Id,
        account.Email,
        account.FullName,
        account.DisplayName,
        account.Phone,
        account.StaffNumber,
        account.IsActive,
        account.UserId,
        account.CreatedAt);
}
