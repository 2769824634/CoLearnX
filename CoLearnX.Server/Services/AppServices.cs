using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Payments;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

// Auth: register / login / switch-role / me. Keep: IAuthService, AuthService
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AvailableRolesDto> GetAvailableRolesAsync(AvailableRolesRequest request, CancellationToken ct = default);
    Task<AuthResponse> SwitchRoleAsync(int userId, SwitchRoleRequest request, CancellationToken ct = default);
    Task<UserMeDto> GetMeAsync(int userId, AppRole activeRole, CancellationToken ct = default);
}

// Profile updates. Keep: IUserService, UserService
public interface IUserService
{
    Task<UserMeDto> UpdateProfileAsync(int userId, AppRole activeRole, UpdateProfileRequest request, CancellationToken ct = default);
}

// Course catalog. Keep: ICourseService, CourseService
public interface ICourseService
{
    Task<IReadOnlyList<CourseListItemDto>> ListAsync(int? userId, string? search, string? category, string? level, bool? featured, CancellationToken ct = default);
    Task<CourseDetailDto?> GetByIdAsync(int courseId, int? userId, CancellationToken ct = default);
    Task<WishlistResultDto> AddToWishlistAsync(int userId, int courseId, CancellationToken ct = default);
    Task<WishlistResultDto> RemoveFromWishlistAsync(int userId, int courseId, CancellationToken ct = default);
}

// Enrol + my enrollments (credits + seats). Keep: IEnrollmentService, EnrollmentService
public interface IEnrollmentService
{
    Task<EnrolResultDto> EnrolAsync(int userId, EnrolRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<EnrollmentDto>> GetMyAsync(int userId, CancellationToken ct = default);
}

// Credit packages, ledger, PayPal. Keep: ICreditService, CreditService
public interface ICreditService
{
    Task<IReadOnlyList<CreditPackageDto>> GetPackagesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CreditLedgerItemDto>> GetMyLedgerAsync(int userId, CancellationToken ct = default);
    Task<CreditLedgerItemDto> TopUpAsync(int userId, TopUpRequest request, CancellationToken ct = default);
    Task<CreatePayPalOrderResponse> CreatePayPalOrderAsync(int userId, CreatePayPalOrderRequest request, CancellationToken ct = default);
    Task<CreditLedgerItemDto> CapturePayPalOrderAsync(int userId, CapturePayPalOrderRequest request, CancellationToken ct = default);
}

// Certificates for current user. Keep: ICertificateService, CertificateService
public interface ICertificateService
{
    Task<IReadOnlyList<CertificateDto>> GetMyAsync(int userId, CancellationToken ct = default);
}

// Materials list. Keep: IMaterialService, MaterialService
public interface IMaterialService
{
    Task<IReadOnlyList<MaterialDto>> ListAsync(MaterialStatus? status, CancellationToken ct = default);
}

// Admin ledger. Keep: IAdminService, AdminService
public interface IAdminService
{
    Task<IReadOnlyList<CreditLedgerItemDto>> GetLedgerAsync(CancellationToken ct = default);
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

public class AuthService(CoLearnXDbContext db, IJwtTokenService jwt) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.FullName.Trim() : request.DisplayName.Trim(),
            CreditBalance = 0,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        db.UserRoles.Add(new UserRole { UserId = user.Id, Role = AppRole.Member, IsVisible = true });
        db.UserPreferences.Add(new UserPreference { UserId = user.Id });
        await db.SaveChangesAsync(ct);

        return await IssueAsync(user.Id, AppRole.Member, ct);
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

    public async Task<AuthResponse> SwitchRoleAsync(int userId, SwitchRoleRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is inactive.");

        var activeRole = RoleParse.Parse(request.ActiveRole);
        if (!user.Roles.Any(r => r.Role == activeRole))
            throw new UnauthorizedAccessException($"Role '{activeRole}' is not enabled for this account.");

        return await IssueAsync(user.Id, activeRole, ct);
    }

    public async Task<UserMeDto> GetMeAsync(int userId, AppRole activeRole, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        return MapMe(user, activeRole);
    }

    private async Task<User> AuthenticateUserAsync(string email, string password, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == normalized, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!user.IsActive || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        return user;
    }

    private async Task<AuthResponse> IssueAsync(int userId, AppRole activeRole, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.Roles).FirstAsync(u => u.Id == userId, ct);
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
            visibility);
    }
}

public class UserService(CoLearnXDbContext db) : IUserService
{
    public async Task<UserMeDto> UpdateProfileAsync(int userId, AppRole activeRole, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Preference)
            .Include(u => u.TrainerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.DisplayName)) user.DisplayName = request.DisplayName.Trim();
        if (request.Phone is not null) user.Phone = request.Phone;
        if (request.Bio is not null) user.Bio = request.Bio;

        user.Preference ??= new UserPreference { UserId = user.Id };
        if (request.LearningGoals is not null) user.Preference.LearningGoals = request.LearningGoals;
        if (request.EmailNotifications is not null) user.Preference.EmailNotifications = request.EmailNotifications.Value;
        if (request.DarkMode is not null) user.Preference.DarkMode = request.DarkMode.Value;

        if (request.IdentityVisibility is not null)
        {
            foreach (var (key, visible) in request.IdentityVisibility)
            {
                if (!Enum.TryParse<AppRole>(key, true, out var role)) continue;
                var row = user.Roles.FirstOrDefault(r => r.Role == role);
                if (row is not null) row.IsVisible = visible;
            }
        }

        if (user.Roles.Any(r => r.Role == AppRole.Trainer))
        {
            user.TrainerProfile ??= new TrainerProfile { UserId = user.Id };
            if (request.Specialisations is not null) user.TrainerProfile.Specialisations = request.Specialisations;
            if (request.TrainerHeadline is not null) user.TrainerProfile.Headline = request.TrainerHeadline;
        }

        await db.SaveChangesAsync(ct);
        return AuthService.MapMe(user, activeRole);
    }
}

public class CourseService(CoLearnXDbContext db) : ICourseService
{
    public async Task<IReadOnlyList<CourseListItemDto>> ListAsync(
        int? userId, string? search, string? category, string? level, bool? featured, CancellationToken ct = default)
    {
        var query = db.Courses.AsNoTracking()
            .Include(c => c.Trainer)
            .Where(c => c.Status == CourseStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLower();
            query = query.Where(c =>
                c.Code.ToLower().Contains(q) ||
                c.Title.ToLower().Contains(q) ||
                c.Trainer.FullName.ToLower().Contains(q));
        }
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);
        if (!string.IsNullOrWhiteSpace(level))
            query = query.Where(c => c.Level == level);
        if (featured == true)
            query = query.Where(c => c.IsFeatured);

        var wishlist = userId is null
            ? new HashSet<int>()
            : await db.WishlistItems.Where(w => w.UserId == userId).Select(w => w.CourseId).ToHashSetAsync(ct);

        var list = await query.OrderBy(c => c.Code).ToListAsync(ct);
        return list.Select(c => new CourseListItemDto(
            c.Id, c.Code, c.Title, c.Trainer.FullName, c.CreditCost, c.Level, c.Category, c.IsFeatured,
            wishlist.Contains(c.Id))).ToList();
    }

    public async Task<CourseDetailDto?> GetByIdAsync(int courseId, int? userId, CancellationToken ct = default)
    {
        var course = await db.Courses.AsNoTracking()
            .Include(c => c.Trainer)
            .Include(c => c.Intakes.Where(i => i.Status == CourseIntakeStatus.Published)).ThenInclude(i => i.Sessions)
            .Include(c => c.LearningOutcomes)
            .FirstOrDefaultAsync(c => c.Id == courseId && c.Status == CourseStatus.Published, ct);
        if (course is null) return null;

        var inWishlist = userId is not null &&
            await db.WishlistItems.AnyAsync(w => w.UserId == userId && w.CourseId == courseId, ct);
        var enrolled = userId is not null &&
            await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId && e.Status == EnrollmentStatus.Active, ct);

        return new CourseDetailDto(
            course.Id,
            course.Code,
            course.Title,
            course.Description,
            course.Trainer.FullName,
            course.CreditCost,
            course.Level,
            course.Category,
            course.Status.ToString(),
            course.LearningOutcomes.OrderBy(o => o.SortOrder).Select(o => o.Text).ToList(),
            course.Intakes.SelectMany(i => i.Sessions).OrderBy(s => s.StartsAt).Select(s => new CourseSessionDto(
                s.Id, s.Label, s.StartsAt, s.EndsAt, s.PhysicalCapacity, s.SeatsLeft,
                s.CourseIntakeId, s.MeetingLink, s.PhysicalAddress, s.PhysicalCapacity, s.PhysicalBookingDeadline)).ToList(),
            inWishlist,
            enrolled);
    }

    public async Task<WishlistResultDto> AddToWishlistAsync(int userId, int courseId, CancellationToken ct = default)
    {
        var course = await RequirePublishedCourseAsync(courseId, ct);
        var exists = await db.WishlistItems.AnyAsync(item => item.UserId == userId && item.CourseId == course.Id, ct);
        if (!exists)
        {
            db.WishlistItems.Add(new WishlistItem { UserId = userId, CourseId = course.Id });
            await db.SaveChangesAsync(ct);
        }

        return new WishlistResultDto(course.Id, true);
    }

    public async Task<WishlistResultDto> RemoveFromWishlistAsync(int userId, int courseId, CancellationToken ct = default)
    {
        var course = await RequirePublishedCourseAsync(courseId, ct);
        var item = await db.WishlistItems.FirstOrDefaultAsync(row => row.UserId == userId && row.CourseId == course.Id, ct);
        if (item is not null)
        {
            db.WishlistItems.Remove(item);
            await db.SaveChangesAsync(ct);
        }

        return new WishlistResultDto(course.Id, false);
    }

    private async Task<Course> RequirePublishedCourseAsync(int courseId, CancellationToken ct)
        => await db.Courses.FirstOrDefaultAsync(course => course.Id == courseId && course.Status == CourseStatus.Published, ct)
            ?? throw new KeyNotFoundException("Course not found.");
}

public class EnrollmentService(CoLearnXDbContext db) : IEnrollmentService
{
    public async Task<EnrolResultDto> EnrolAsync(int userId, EnrolRequest request, CancellationToken ct = default)
    {
        // BR-04: only Member can enrol — enforced by controller Authorize(Roles=Member)
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId && c.Status == CourseStatus.Published, ct)
            ?? throw new InvalidOperationException("Course not available.");
        // Legacy Session-based adapter only. A still owns the full Intake enrolment conversion.
        var session = await db.CourseSessions.FirstOrDefaultAsync(s => s.Id == request.CourseSessionId
            && s.CourseIntake.CourseId == course.Id && s.CourseIntake.Status == CourseIntakeStatus.Published, ct)
            ?? throw new InvalidOperationException("Session not found.");

        if (session.SeatsTaken >= session.PhysicalCapacity)
            throw new InvalidOperationException("Session is full."); // BR-05

        if (await db.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == course.Id && e.Status == EnrollmentStatus.Active, ct))
            throw new InvalidOperationException("Already enrolled.");

        if (user.CreditBalance < course.CreditCost)
            throw new InvalidOperationException("INSUFFICIENT_CREDITS"); // BR-01

        user.CreditBalance -= course.CreditCost;
        session.SeatsTaken += 1;

        var enrollment = new Enrollment
        {
            UserId = userId,
            CourseId = course.Id,
            CourseSessionId = session.Id,
            Status = EnrollmentStatus.Active,
            ProgressPercent = 0,
            CreditsSpent = course.CreditCost,
        };
        db.Enrollments.Add(enrollment);

        // BR-02
        db.CreditTransactions.Add(new CreditTransaction
        {
            UserId = userId,
            Type = CreditTransactionType.Enrolment,
            Description = $"{course.Code} — {course.Title}",
            Delta = -course.CreditCost,
            BalanceAfter = user.CreditBalance,
        });

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Code = "N-01",
            Title = "Enrolment confirmed",
            Body = $"You enrolled in {course.Code}.",
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new EnrolResultDto(enrollment.Id, course.CreditCost, user.CreditBalance, course.Code, course.Title);
    }

    public async Task<IReadOnlyList<EnrollmentDto>> GetMyAsync(int userId, CancellationToken ct = default)
    {
        return await db.Enrollments.AsNoTracking()
            .Include(e => e.Course).ThenInclude(c => c.Trainer)
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new EnrollmentDto(
                e.Id,
                e.CourseId,
                e.Course.Code,
                e.Course.Title,
                e.Course.Trainer.FullName,
                e.Status.ToString(),
                e.ProgressPercent,
                e.CourseSessionId))
            .ToListAsync(ct);
    }
}

public class CreditService(CoLearnXDbContext db, IPayPalClient payPal, Microsoft.Extensions.Options.IOptions<PayPalOptions> payPalOptions) : ICreditService
{
    public async Task<IReadOnlyList<CreditPackageDto>> GetPackagesAsync(CancellationToken ct = default)
    {
        return await db.CreditPackages.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => new CreditPackageDto(
                p.Id,
                p.PayAudCents,
                p.PayAudCents / 100m,
                p.Credits,
                p.Note,
                p.IsBestValue))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CreditLedgerItemDto>> GetMyLedgerAsync(int userId, CancellationToken ct = default)
    {
        return await db.CreditTransactions.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new CreditLedgerItemDto(t.Id, t.CreatedAt, t.Type.ToString(), t.Description, t.Delta, t.BalanceAfter))
            .ToListAsync(ct);
    }

    public async Task<CreditLedgerItemDto> TopUpAsync(int userId, TopUpRequest request, CancellationToken ct = default)
    {
        // Dev-only simulated top-up kept for offline demos. Prefer PayPal create/capture in UI.
        var package = await db.CreditPackages.FirstOrDefaultAsync(p => p.Id == request.CreditPackageId && p.IsActive, ct)
            ?? throw new InvalidOperationException("Invalid credit package.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);

        var payment = new PaymentTransaction
        {
            UserId = userId,
            CreditPackageId = package.Id,
            Provider = "PayPal-Sim",
            ProviderReference = request.ProviderReference ?? $"SIM-{Guid.NewGuid():N}",
            AmountAudCents = package.PayAudCents,
            CreditsGranted = package.Credits,
            Status = PaymentStatus.Completed,
        };
        db.PaymentTransactions.Add(payment);

        user.CreditBalance += package.Credits;
        var ledger = new CreditTransaction
        {
            UserId = userId,
            Type = CreditTransactionType.TopUp,
            Description = $"Simulated PayPal package ${package.PayAudCents / 100m:0} → +{package.Credits}",
            Delta = package.Credits,
            BalanceAfter = user.CreditBalance,
            RelatedPaymentId = payment.Id,
        };
        db.CreditTransactions.Add(ledger);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new CreditLedgerItemDto(ledger.Id, ledger.CreatedAt, ledger.Type.ToString(), ledger.Description, ledger.Delta, ledger.BalanceAfter);
    }

    public async Task<CreatePayPalOrderResponse> CreatePayPalOrderAsync(int userId, CreatePayPalOrderRequest request, CancellationToken ct = default)
    {
        var options = payPalOptions.Value;
        var package = await db.CreditPackages.FirstOrDefaultAsync(p => p.Id == request.CreditPackageId && p.IsActive, ct)
            ?? throw new InvalidOperationException("Invalid credit package.");

        var amount = package.PayAudCents / 100m;
        var customId = $"u{userId}:p{package.Id}";
        var description = $"CoLearnX credits x{package.Credits}";
        var orderId = await payPal.CreateOrderAsync(amount, options.Currency, customId, description, ct);

        db.PaymentTransactions.Add(new PaymentTransaction
        {
            UserId = userId,
            CreditPackageId = package.Id,
            Provider = "PayPal",
            ProviderReference = orderId,
            AmountAudCents = package.PayAudCents,
            CreditsGranted = package.Credits,
            Status = PaymentStatus.Pending,
        });
        await db.SaveChangesAsync(ct);

        return new CreatePayPalOrderResponse(orderId, package.Id, amount, options.Currency);
    }

    public async Task<CreditLedgerItemDto> CapturePayPalOrderAsync(int userId, CapturePayPalOrderRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
            throw new InvalidOperationException("OrderId is required.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var payment = await db.PaymentTransactions
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ProviderReference == request.OrderId && p.Provider == "PayPal", ct)
            ?? throw new InvalidOperationException("Payment order not found.");

        if (payment.Status == PaymentStatus.Completed)
        {
            var existing = await db.CreditTransactions
                .Where(t => t.RelatedPaymentId == payment.Id)
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync(ct);
            if (existing is not null)
            {
                await tx.CommitAsync(ct);
                return new CreditLedgerItemDto(existing.Id, existing.CreatedAt, existing.Type.ToString(), existing.Description, existing.Delta, existing.BalanceAfter);
            }
        }

        var (ok, status, captureId) = await payPal.CaptureOrderAsync(request.OrderId, ct);
        if (!ok)
            throw new InvalidOperationException($"PayPal capture status was {status}.");

        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        user.CreditBalance += payment.CreditsGranted;
        payment.Status = PaymentStatus.Completed;

        var ledger = new CreditTransaction
        {
            UserId = userId,
            Type = CreditTransactionType.TopUp,
            Description = string.IsNullOrWhiteSpace(captureId)
                ? $"PayPal package ${payment.AmountAudCents / 100m:0} → +{payment.CreditsGranted}"
                : $"PayPal package ${payment.AmountAudCents / 100m:0} → +{payment.CreditsGranted} (capture {captureId})",
            Delta = payment.CreditsGranted,
            BalanceAfter = user.CreditBalance,
            RelatedPaymentId = payment.Id,
        };
        db.CreditTransactions.Add(ledger);

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Code = "N-topup",
            Title = "Credits topped up",
            Body = $"+{payment.CreditsGranted} credits via PayPal.",
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new CreditLedgerItemDto(ledger.Id, ledger.CreatedAt, ledger.Type.ToString(), ledger.Description, ledger.Delta, ledger.BalanceAfter);
    }
}

public class CertificateService(CoLearnXDbContext db) : ICertificateService
{
    public async Task<IReadOnlyList<CertificateDto>> GetMyAsync(int userId, CancellationToken ct = default)
    {
        return await db.UserCertificates.AsNoTracking()
            .Include(c => c.CertificateTemplate)
            .Where(c => c.UserId == userId && c.AdminApproved)
            .OrderBy(c => c.CertificateTemplate.StageNumber)
            .Select(c => new CertificateDto(
                c.Id,
                c.CertificateTemplate.StageName,
                c.CertificateTemplate.StageNumber,
                c.CertificateTemplate.Title,
                c.AwardedAt,
                c.VerificationCode))
            .ToListAsync(ct);
    }
}

public class MaterialService(CoLearnXDbContext db) : IMaterialService
{
    public async Task<IReadOnlyList<MaterialDto>> ListAsync(MaterialStatus? status, CancellationToken ct = default)
    {
        var query = db.LearningMaterials.AsNoTracking().Include(m => m.Creator).AsQueryable();
        if (status is not null) query = query.Where(m => m.Status == status);

        return await query.OrderByDescending(m => m.CreatedAt)
            .Select(m => new MaterialDto(m.Id, m.Title, m.Format, m.Category, m.Status.ToString(), m.Creator.FullName, m.Version))
            .ToListAsync(ct);
    }
}

public class AdminService(CoLearnXDbContext db) : IAdminService
{
    public async Task<IReadOnlyList<CreditLedgerItemDto>> GetLedgerAsync(CancellationToken ct = default)
    {
        return await db.CreditTransactions.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new CreditLedgerItemDto(t.Id, t.CreatedAt, t.Type.ToString(), t.Description, t.Delta, t.BalanceAfter))
            .ToListAsync(ct);
    }
}
