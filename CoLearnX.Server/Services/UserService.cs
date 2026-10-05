using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IUserService
{
    Task<UserMeDto> UpdateProfileAsync(int userId, AppRole activeRole, UpdateProfileRequest request, CancellationToken ct = default);
    Task<UserMeDto> UploadAvatarAsync(int userId, AppRole activeRole, IFormFile? file, CancellationToken ct = default);
    Task<MaterialFileResult?> OpenAvatarAsync(int userId, CancellationToken ct = default);
}

public class UserService(CoLearnXDbContext db, IFileStorage files) : IUserService
{
    public async Task<UserMeDto> UploadAvatarAsync(int userId, AppRole activeRole, IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("Choose an image to upload.");
        if (file.Length > 2 * 1024 * 1024)
            throw new ArgumentException("Avatar must be 2 MB or smaller.");

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        if (buffer.Length > 2 * 1024 * 1024)
            throw new ArgumentException("Avatar must be 2 MB or smaller.");
        var bytes = buffer.ToArray();
        var (extension, contentType) = AvatarImageType(bytes);
        var user = await db.Users.Include(item => item.Roles)
            .Include(item => item.Preference).Include(item => item.TrainerProfile).Include(item => item.CreatorProfile)
            .SingleOrDefaultAsync(item => item.Id == userId && item.IsActive, ct)
            ?? throw new KeyNotFoundException("User not found.");
        var key = $"avatars/{userId}/{Guid.NewGuid():N}{extension}";
        using var content = new MemoryStream(bytes);
        await files.SaveAsync(key, content, contentType, ct);
        user.AvatarUrl = key;
        await db.SaveChangesAsync(ct);
        return AuthService.MapMe(user, activeRole);
    }

    public async Task<MaterialFileResult?> OpenAvatarAsync(int userId, CancellationToken ct = default)
    {
        var key = await db.Users.Where(item => item.Id == userId && item.IsActive)
            .Select(item => item.AvatarUrl).SingleOrDefaultAsync(ct);
        if (key is null) return null;
        var stream = await files.OpenAsync(key, ct);
        if (stream is null) return null;
        var contentType = Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
        return new MaterialFileResult(stream, contentType, "avatar" + Path.GetExtension(key));
    }

    private static (string Extension, string ContentType) AvatarImageType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return (".png", "image/png");
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255)
            return (".jpg", "image/jpeg");
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return (".webp", "image/webp");
        throw new ArgumentException("Upload a PNG, JPEG or WebP image.");
    }

    public async Task<UserMeDto> UpdateProfileAsync(int userId, AppRole activeRole, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .Include(u => u.Preference)
            .Include(u => u.TrainerProfile)
            .Include(u => u.CreatorProfile)
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

        if (user.Roles.Any(r => r.Role == AppRole.Creator))
        {
            user.CreatorProfile ??= new CreatorProfile { UserId = user.Id };
            if (request.ExpertiseTags is not null) user.CreatorProfile.ExpertiseTags = request.ExpertiseTags;
            if (request.CreatorHeadline is not null) user.CreatorProfile.Headline = request.CreatorHeadline;
        }

        await db.SaveChangesAsync(ct);
        return AuthService.MapMe(user, activeRole);
    }
}
