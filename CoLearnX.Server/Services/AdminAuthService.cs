using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IAdminAuthService
{
    Task<AdminAuthResponse> LoginAsync(AdminLoginRequest request, CancellationToken ct = default);
    Task<AdminAccountDto> GetMeAsync(int adminAccountId, CancellationToken ct = default);
}

public class AdminAuthService(
    CoLearnXDbContext db,
    IAdminTokenService tokens,
    IAuditLogService auditLogs) : IAdminAuthService
{
    public async Task<AdminAuthResponse> LoginAsync(AdminLoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var admin = await db.AdminAccounts.SingleOrDefaultAsync(account => account.Email == email, ct);

        if (admin is null || !admin.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, admin.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        await auditLogs.AppendAdminAsync(
            admin.Id,
            "AdminLogin",
            nameof(Domain.Entities.AdminAccount),
            admin.Id.ToString(),
            "Succeeded",
            ct: ct);

        var (token, expiresAt) = tokens.CreateToken(admin);
        return new AdminAuthResponse(token, expiresAt, Map(admin));
    }

    public async Task<AdminAccountDto> GetMeAsync(int adminAccountId, CancellationToken ct = default)
    {
        var admin = await db.AdminAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(account => account.Id == adminAccountId && account.IsActive, ct)
            ?? throw new UnauthorizedAccessException("Administrator account is unavailable.");

        return Map(admin);
    }

    private static AdminAccountDto Map(Domain.Entities.AdminAccount admin)
        => new(admin.Id, admin.Email);
}
