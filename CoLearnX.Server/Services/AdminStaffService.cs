using System.Security.Cryptography;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IAdminStaffService
{
    Task<StaffDetailDto?> DetailAsync(int id, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string Name)?> OpenDocumentAsync(int id, CancellationToken ct = default);
    Task<StaffPasswordResetDto?> ResetPasswordAsync(int adminId, int id, CancellationToken ct = default);
    Task<IReadOnlyList<StaffAccountDto>> ListAsync(CancellationToken ct = default);
    Task<StaffProvisioningResultDto> ProvisionAsync(
        int adminAccountId,
        ProvisionStaffRequest request,
        CancellationToken ct = default);
    Task<StaffAccountDto> SetActiveAsync(
        int adminAccountId,
        int staffAccountId,
        bool isActive,
        CancellationToken ct = default);
}

public sealed class AdminStaffService(CoLearnXDbContext db, IRoleRequestFileStorage files) : IAdminStaffService
{
    public async Task<IReadOnlyList<StaffAccountDto>> ListAsync(CancellationToken ct = default)
    {
        var accounts = await db.StaffAccounts.AsNoTracking()
            .OrderBy(account => account.StaffNumber)
            .ToListAsync(ct);
        return accounts.Select(Map).ToList();
    }

    public async Task<StaffProvisioningResultDto> ProvisionAsync(
        int adminAccountId,
        ProvisionStaffRequest request,
        CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.StaffAccounts.AnyAsync(account => account.Email == email, ct)
            || await db.Users.AnyAsync(user => user.Email == email, ct)
            || await db.AdminAccounts.AnyAsync(account => account.Email == email, ct))
            throw new StaffAccountException("STAFF_EMAIL_EXISTS", "This email is already in use.");

        var document = request.Document
            ?? throw new StaffAccountException("STAFF_DOCUMENT_REQUIRED", "A staff document is required.");
        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
        if (extension is not ".pdf" and not ".png" and not ".jpg" and not ".jpeg")
            throw new StaffAccountException("INVALID_STAFF_DOCUMENT", "Staff document must be PDF, PNG or JPG.");
        if (document.Length is <= 0 or > 5 * 1024 * 1024)
            throw new StaffAccountException("INVALID_STAFF_DOCUMENT", "Staff document must be between 1 byte and 5 MB.");

        var staffNumber = (await db.StaffAccounts.MaxAsync(account => (int?)account.StaffNumber, ct) ?? 0) + 1;
        var displayName = $"CoLearnX 讲师 {staffNumber:000}";
        var documentPath = $"staff/{staffNumber}/document{extension}";
        var initialPassword = CreateInitialPassword();
        await using var documentStream = document.OpenReadStream();
        await files.SaveAsync(documentPath, documentStream, RoleRequestFiles.ContentType(extension), ct);

        // Azure SQL enables retry-on-failure; user transactions must run inside that strategy.
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    var now = DateTime.UtcNow;
                    var hiddenUser = new User
                    {
                        Email = $"staff-{staffNumber}@staff.colearnx",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(CreateInitialPassword()),
                        FullName = displayName,
                        DisplayName = displayName,
                        CreditBalance = 0,
                        HeldCredits = 0,
                        IsActive = true,
                        EmailVerifiedAt = now,
                        SessionStamp = Guid.NewGuid(),
                    };
                    db.Users.Add(hiddenUser);
                    await db.SaveChangesAsync(ct);

                    db.UserRoles.Add(new UserRole
                    {
                        UserId = hiddenUser.Id,
                        Role = AppRole.Trainer,
                        IsVisible = false,
                    });
                    db.TrainerProfiles.Add(new TrainerProfile { UserId = hiddenUser.Id });

                    var account = new StaffAccount
                    {
                        Email = email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(initialPassword),
                        FullName = request.FullName.Trim(),
                        DisplayName = displayName,
                        Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                        StaffNumber = staffNumber,
                        DocumentPath = documentPath,
                        UserId = hiddenUser.Id,
                        CreatedByAdminAccountId = adminAccountId,
                        SessionStamp = Guid.NewGuid(),
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    db.StaffAccounts.Add(account);
                    await db.SaveChangesAsync(ct);

                    db.AuditLogs.Add(new AuditLog
                    {
                        AdminAccountId = adminAccountId,
                        Action = "StaffAccountProvisioned",
                        EntityType = nameof(StaffAccount),
                        EntityId = account.Id.ToString(),
                        Result = "Succeeded",
                    });
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return new StaffProvisioningResultDto(Map(account), initialPassword);
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            });
        }
        catch
        {
            await files.DeleteAsync(documentPath, ct);
            throw;
        }
    }

    public async Task<StaffAccountDto> SetActiveAsync(
        int adminAccountId,
        int staffAccountId,
        bool isActive,
        CancellationToken ct = default)
    {
        var account = await db.StaffAccounts.Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == staffAccountId, ct)
            ?? throw new KeyNotFoundException("Staff account not found.");
        account.IsActive = isActive;
        account.User.IsActive = isActive;
        account.SessionStamp = Guid.NewGuid();
        account.User.SessionStamp = Guid.NewGuid();
        account.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            AdminAccountId = adminAccountId,
            Action = isActive ? "StaffAccountActivated" : "StaffAccountDeactivated",
            EntityType = nameof(StaffAccount),
            EntityId = account.Id.ToString(),
            Result = "Succeeded",
        });
        await db.SaveChangesAsync(ct);
        return Map(account);
    }

    public async Task<StaffDetailDto?> DetailAsync(int id, CancellationToken ct = default)
    {
        var account = await db.StaffAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        if (account is null) return null;
        var courses = await db.Courses.AsNoTracking().Where(c => c.TrainerId == account.UserId)
            .OrderBy(c => c.Title).Select(c => new StaffCourseSummaryDto(c.Id, c.Code, c.Title, c.Status.ToString())).ToListAsync(ct);
        var intakes = await db.CourseIntakes.AsNoTracking().Where(i => i.TrainerId == account.UserId)
            .OrderByDescending(i => i.StartsAt)
            .Select(i => new StaffIntakeSummaryDto(i.Id, i.CourseId, i.Course.Title, i.Status.ToString(), i.StartsAt, i.EndsAt)).ToListAsync(ct);
        return new StaffDetailDto(Map(account), !string.IsNullOrEmpty(account.DocumentPath),
            string.IsNullOrEmpty(account.DocumentPath) ? null : Path.GetFileName(account.DocumentPath), courses, intakes);
    }

    public async Task<(Stream Stream, string ContentType, string Name)?> OpenDocumentAsync(int id, CancellationToken ct = default)
    {
        var account = await db.StaffAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
        if (account is null || string.IsNullOrEmpty(account.DocumentPath)) return null;
        var stream = await files.OpenAsync(account.DocumentPath, ct);
        if (stream is null) return null;
        return (stream, RoleRequestFiles.ContentType(Path.GetExtension(account.DocumentPath)), Path.GetFileName(account.DocumentPath));
    }

    public async Task<StaffPasswordResetDto?> ResetPasswordAsync(int adminId, int id, CancellationToken ct = default)
    {
        var account = await db.StaffAccounts.Include(a => a.User).SingleOrDefaultAsync(a => a.Id == id, ct);
        if (account is null) return null;
        var password = CreateInitialPassword();
        account.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        account.SessionStamp = Guid.NewGuid();
        account.User.SessionStamp = Guid.NewGuid();
        account.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog { AdminAccountId = adminId, Action = "StaffPasswordReset",
            EntityType = nameof(StaffAccount), EntityId = id.ToString(), Result = "Succeeded" });
        await db.SaveChangesAsync(ct);
        return new StaffPasswordResetDto(password);
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

    private static string CreateInitialPassword()
        => $"Stf!{Convert.ToHexString(RandomNumberGenerator.GetBytes(10))}a1";
}

public sealed class StaffAccountException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
