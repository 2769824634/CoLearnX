using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CoLearnX.Server.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AvailableRolesDto> GetAvailableRolesAsync(AvailableRolesRequest request, CancellationToken ct = default);
    Task<AuthResponse> SwitchRoleAsync(int userId, SwitchRoleRequest request, CancellationToken ct = default);
    Task<UserMeDto> GetMeAsync(int userId, AppRole activeRole, CancellationToken ct = default);
}

public static class RoleParse
{
    public static AppRole Parse(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new InvalidOperationException("Active role is required.");
        if (!Enum.TryParse<AppRole>(role, true, out var parsed))
            throw new InvalidOperationException($"Unknown role '{role}'.");
        return parsed;
    }
}

public class AuthService(
    CoLearnXDbContext db,
    IJwtTokenService jwt,
    EmailVerificationService emailVerification,
    IOptions<PasswordResetOptions> mailOptions,
    IWebHostEnvironment environment) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var taken = await db.Users.AnyAsync(u => u.Email == email, ct)
            || await db.AdminAccounts.AnyAsync(account => account.Email == email, ct);
        if (taken)
            throw new InvalidOperationException(PasswordRules.TakenMessage);
        if (!PasswordRules.Meets(request.Password, email))
            throw new FormatException(PasswordRules.Hint);

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            FullName = request.FullName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.FullName.Trim() : request.DisplayName.Trim(),
            CreditBalance = 0,
            EmailVerifiedAt = null,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        db.UserRoles.Add(new UserRole { UserId = user.Id, Role = AppRole.Member, IsVisible = true });
        db.UserPreferences.Add(new UserPreference { UserId = user.Id });
        await db.SaveChangesAsync(ct);
        if (!PasswordResetOptions.CanDeliver(mailOptions.Value, environment))
        {
            user.EmailVerifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new AuthResponse(string.Empty, DateTime.UtcNow, MapMe(user, AppRole.Member));
        }
        await emailVerification.RequestAsync(user, ct);

        return new AuthResponse(
            string.Empty,
            DateTime.UtcNow,
            MapMe(user, AppRole.Member),
            EmailVerificationRequired: true,
            Message: "Check your email to verify your account before signing in.");
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await AuthenticateUserAsync(request.Email, request.Password, ct);

        if (string.Equals(request.ActiveRole, "Admin", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Administrators must use the operations sign-in.");

        var activeRole = RoleParse.Parse(request.ActiveRole);
        if (!user.Roles.Any(r => r.Role == activeRole))
        {
            throw new UnauthorizedAccessException($"Role '{activeRole}' is not enabled for this account.");
        }

        return await IssueAsync(user.Id, activeRole, rotateSession: true, ct);
    }

    public async Task<AvailableRolesDto> GetAvailableRolesAsync(AvailableRolesRequest request, CancellationToken ct = default)
    {
        var user = await AuthenticateUserAsync(request.Email, request.Password, ct);
        return new AvailableRolesDto(user.Roles
            .Select(role => role.Role.ToString())
            .Distinct()
            .OrderBy(role => role)
            .ToList());
    }

    public async Task<AuthResponse> SwitchRoleAsync(int userId, SwitchRoleRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is inactive.");

        var activeRole = RoleParse.Parse(request.ActiveRole);
        if (!user.Roles.Any(r => r.Role == activeRole))
            throw new UnauthorizedAccessException($"Role '{activeRole}' is not enabled for this account.");

        return await IssueAsync(user.Id, activeRole, rotateSession: false, ct);
    }

    public async Task<UserMeDto> GetMeAsync(int userId, AppRole activeRole, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Preference)
            .Include(u => u.TrainerProfile)
            .Include(u => u.CreatorProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        return MapMe(user, activeRole);
    }

    private async Task<User> AuthenticateUserAsync(string email, string password, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var admin = await db.AdminAccounts.AsNoTracking()
            .FirstOrDefaultAsync(account => account.Email == normalized, ct);
        if (admin is not null)
        {
            if (admin.IsActive && BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
                throw new UnauthorizedAccessException("Administrators must use the operations sign-in.");
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == normalized, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");
        if (user.EmailVerifiedAt is null)
        {
            if (!PasswordResetOptions.CanDeliver(mailOptions.Value, environment))
            {
                user.EmailVerifiedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            else
                throw new UnauthorizedAccessException("Verify your email before signing in.");
        }

        return user;
    }

    private async Task<AuthResponse> IssueAsync(int userId, AppRole activeRole, bool rotateSession, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Preference)
            .Include(u => u.TrainerProfile)
            .Include(u => u.CreatorProfile)
            .FirstAsync(u => u.Id == userId, ct);
        if (rotateSession || user.SessionStamp == Guid.Empty)
        {
            user.SessionStamp = SessionStampValidator.NewStamp();
            await db.SaveChangesAsync(ct);
        }

        var roles = user.Roles.Select(r => r.Role).ToList();
        var (token, expires) = jwt.CreateToken(user, activeRole, roles);
        return new AuthResponse(token, expires, MapMe(user, activeRole));
    }

    internal static UserMeDto MapMe(User user, AppRole activeRole)
    {
        var visibility = user.Roles.ToDictionary(
            r => r.Role.ToString().ToLowerInvariant(),
            r => r.IsVisible);

        return new UserMeDto(
            user.Id,
            user.Email,
            user.FullName,
            user.DisplayName,
            user.Phone,
            user.Bio,
            user.CreditBalance,
            activeRole.ToString(),
            user.Roles.Select(r => r.Role.ToString()).OrderBy(x => x).ToList(),
            visibility,
            user.Preference?.LearningGoals,
            user.TrainerProfile?.Specialisations,
            user.TrainerProfile?.Headline,
            user.CreatorProfile?.ExpertiseTags,
            user.CreatorProfile?.Headline,
            user.Preference?.EmailNotifications ?? true,
            user.AvatarUrl is null ? null : $"/api/users/{user.Id}/avatar?v={Path.GetFileNameWithoutExtension(user.AvatarUrl)}",
            user.Preference?.OnboardingCompletedAt,
            user.Preference?.OnboardingSkippedAt,
            user.HeldCredits,
            user.CreditBalance + user.HeldCredits);
    }
}
