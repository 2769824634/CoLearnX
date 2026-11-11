using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain;
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

public sealed class RegistrationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
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
        if (PlatformIdentity.IsInternalEmail(email))
            throw new InvalidOperationException(PasswordRules.TakenMessage);
        var taken = await db.Users.AnyAsync(u => u.Email == email, ct)
            || await db.AdminAccounts.AnyAsync(account => account.Email == email, ct)
            || await db.StaffAccounts.AnyAsync(account => account.Email == email, ct);
        if (taken)
            throw new InvalidOperationException(PasswordRules.TakenMessage);
        if (!PasswordRules.Meets(request.Password, email))
            throw new FormatException(PasswordRules.Hint);
        var role = ParseRegistrationRole(request);

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

        db.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            Role = role,
            CertifiedAt = role == AppRole.Member ? DateTime.UtcNow : null,
        });
        if (role == AppRole.Trainer)
            db.TrainerProfiles.Add(new TrainerProfile { UserId = user.Id });
        else if (role == AppRole.Creator)
            db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id });
        db.UserPreferences.Add(new UserPreference { UserId = user.Id });
        await db.SaveChangesAsync(ct);
        if (!PasswordResetOptions.CanDeliver(mailOptions.Value, environment))
        {
            user.EmailVerifiedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new AuthResponse(string.Empty, DateTime.UtcNow, MapMe(user, role));
        }
        await emailVerification.RequestAsync(user, ct);

        return new AuthResponse(
            string.Empty,
            DateTime.UtcNow,
            MapMe(user, role),
            EmailVerificationRequired: true,
            Message: "Check your email to verify your account before signing in.");
    }

    private static AppRole ParseRegistrationRole(RegisterRequest request)
    {
        if (!Enum.TryParse<AppRole>(request.Role, true, out var role)
            || role is not (AppRole.Member or AppRole.Trainer or AppRole.Creator)
            || int.TryParse(request.Role, out _))
            throw new RegistrationException("ROLE_NOT_ALLOWED", "Choose Member, Trainer or Creator.");
        return role;
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

        return await IssueAsync(user.Id, activeRole, ct);
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

    public async Task<UserMeDto> GetMeAsync(int userId, AppRole activeRole, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Preference)
            .Include(u => u.TrainerProfile)
            .Include(u => u.CreatorProfile)
            .Include(u => u.Specialties).ThenInclude(item => item.CourseSubject)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        return MapMe(user, activeRole);
    }

    private async Task<User> AuthenticateUserAsync(string email, string password, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (PlatformIdentity.IsInternalEmail(normalized))
            throw new UnauthorizedAccessException("Invalid email or password.");
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

    private async Task<AuthResponse> IssueAsync(int userId, AppRole activeRole, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Preference)
            .Include(u => u.TrainerProfile)
            .Include(u => u.CreatorProfile)
            .Include(u => u.Specialties).ThenInclude(item => item.CourseSubject)
            .FirstAsync(u => u.Id == userId, ct);
        user.SessionStamp = SessionStampValidator.NewStamp();
        await db.SaveChangesAsync(ct);

        var roles = user.Roles.Select(r => r.Role).ToList();
        var (token, expires) = jwt.CreateToken(user, activeRole, roles);
        return new AuthResponse(token, expires, MapMe(user, activeRole));
    }

    internal static UserMeDto MapMe(User user, AppRole activeRole)
    {
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
            user.CreditBalance + user.HeldCredits,
            user.Country,
            user.Occupation,
            user.Roles.FirstOrDefault(r => r.Role == activeRole)?.CertifiedAt,
            activeRole is AppRole.Trainer or AppRole.Creator
                ? user.Specialties.Where(item => item.CourseSubject is not null)
                    .Select(item => item.CourseSubject.Name).OrderBy(name => name).ToList()
                : null);
    }
}
