using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IMaterialService
{
    Task<IReadOnlyList<MaterialDto>> ListAsync(int userId, MaterialStatus? status, int? courseId = null, CancellationToken ct = default);
    Task<IReadOnlyList<CreatorMaterialUsageDto>> ListCreatorUsageAsync(int creatorId, CancellationToken ct = default);
    Task<MaterialDto> UploadAsync(int creatorId, int courseId, string title, string? category, string? description, IFormFile? file, CancellationToken ct = default);
    Task<MaterialFileResult> OpenDownloadAsync(int userId, int materialId, CancellationToken ct = default);
    StorageStatusDto GetStorageStatus();
    Task<MaterialCloudLinkDto> CreateCloudLinkAsync(int userId, int materialId, TimeSpan lifetime, CancellationToken ct = default);
}

public class MaterialService(CoLearnXDbContext db, IFileStorage files, IMaterialVersionService versions,
    ILogger<MaterialService>? logger = null) : IMaterialService
{
    public async Task<IReadOnlyList<CreatorMaterialUsageDto>> ListCreatorUsageAsync(int creatorId, CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(user => user.Id == creatorId && user.IsActive
            && user.Roles.Any(role => role.Role == AppRole.Creator), ct))
            throw new LaterPhaseException("CREATOR_REQUIRED", "An active Creator account is required.", 403);

        return await (from log in db.MaterialUsageLogs.AsNoTracking()
                  join course in db.Courses.AsNoTracking() on log.CourseId equals course.Id
                  join trainer in db.Users.AsNoTracking() on log.TrainerId equals trainer.Id
                  join intake in db.CourseIntakes.AsNoTracking() on log.CourseIntakeId equals (int?)intake.Id into intakes
                  from intake in intakes.DefaultIfEmpty()
                  where log.LearningMaterial.CreatorId == creatorId
                  orderby log.UsedAt descending, log.Id descending
                  select new CreatorMaterialUsageDto(
                      log.Id, log.LearningMaterialId, log.LearningMaterial.Title,
                      course.Id, course.Code, course.Title,
                      trainer.Id, trainer.FullName, log.UsedAt,
                      log.CourseIntakeId, intake == null ? null : intake.StartsAt, intake == null ? null : intake.EndsAt)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MaterialDto>> ListAsync(int userId, MaterialStatus? status, int? courseId = null, CancellationToken ct = default)
    {
        var query = db.LearningMaterials.AsNoTracking().Include(m => m.Creator)
            .Where(m => m.CreatorId == userId || m.Status == MaterialStatus.Approved);
        if (status is not null) query = query.Where(m => m.Status == status);
        if (courseId is int id)
            query = query.Where(m => m.CourseMaterials.Any(link => link.CourseId == id));

        return await query.OrderByDescending(m => m.CreatedAt)
            .Select(m => new MaterialDto(
                m.Id,
                m.Title,
                m.Format,
                m.Category,
                m.Status.ToString(),
                m.Creator.FullName,
                m.Version,
                m.CourseMaterials.Select(link => link.CourseId).FirstOrDefault(),
                m.CourseMaterials.Select(link => link.Course.Code).FirstOrDefault(),
                m.CourseMaterials.Select(link => link.Course.Title).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<MaterialDto> UploadAsync(int creatorId, int courseId, string title, string? category, string? description, IFormFile? file, CancellationToken ct = default)
    {
        if (courseId <= 0)
            throw new LaterPhaseException("COURSE_REQUIRED", "Choose the Course this material belongs to.", field: "courseId");
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Title is required.");
        if (file is null || file.Length <= 0)
            throw new InvalidOperationException("A file is required.");
        if (file.Length > MaterialFiles.MaxBytes)
            throw new InvalidOperationException("File must be 20 MB or smaller.");

        var ext = MaterialFiles.RequireSafeExtension(file.FileName);
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == creatorId, ct))
            throw new InvalidOperationException("Creator not found.");
        if (!await db.Courses.AsNoTracking().AnyAsync(c => c.Id == courseId && c.CreatorId == creatorId, ct))
            throw new LaterPhaseException("COURSE_NOT_FOUND", "Course was not found.", 404, "courseId");

        var key = $"materials/{creatorId}/{Guid.NewGuid():N}{ext}";
        MaterialVersionDto submitted;
        try
        {
            await using (var stream = file.OpenReadStream())
                await files.SaveAsync(key, stream, MaterialFiles.ContentType(ext), ct);
            submitted = await versions.CreateAsync(
                creatorId,
                new CreateMaterialVersionRequest(
                    title.Trim(),
                    string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                    key,
                    MaterialFiles.FormatLabel(ext),
                    string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                    courseId),
                ct);
        }
        catch
        {
            // A request cancellation must not cancel compensation for a partially saved upload.
            // A lost commit acknowledgement may leave a valid database reference; never delete it.
            try
            {
                var referenced = await db.CourseMaterialVersions.AsNoTracking()
                    .AnyAsync(version => version.FilePath == key, CancellationToken.None);
                if (!referenced) await files.DeleteAsync(key, CancellationToken.None);
            }
            catch (Exception cleanupError)
            {
                logger?.LogError(cleanupError, "Material upload cleanup failed for storage key {Key}", key);
            }
            throw;
        }

        return new MaterialDto(
            submitted.LearningMaterialId,
            submitted.Title,
            submitted.Format,
            submitted.Category,
            MaterialStatus.PendingReview.ToString(),
            submitted.CreatorName,
            submitted.VersionNumber,
            submitted.CourseId,
            submitted.CourseCode,
            submitted.CourseTitle);
    }

    public async Task<MaterialFileResult> OpenDownloadAsync(int userId, int materialId, CancellationToken ct = default)
    {
        var access = new MaterialFileAccess(db, files);
        var (filePath, title) = await access.RequireCreatorOwnOrApprovedAsync(userId, materialId, ct);
        return await access.OpenStoredAsync(filePath, title, new FileNotFoundException("Material file not found."), ct);
    }

    public StorageStatusDto GetStorageStatus()
        => new(files.Provider, files.CanIssueCloudLinks, files.Container);

    public async Task<MaterialCloudLinkDto> CreateCloudLinkAsync(int userId, int materialId, TimeSpan lifetime, CancellationToken ct = default)
    {
        var access = new MaterialFileAccess(db, files);
        var (filePath, _) = await access.RequireCreatorOwnOrApprovedAsync(userId, materialId, ct);

        if (!files.CanIssueCloudLinks)
            throw new InvalidOperationException("Cloud links require Azure Blob storage.");

        var uri = await files.TryCreateReadUriAsync(filePath, lifetime, ct)
            ?? throw new FileNotFoundException("Material file not found.");

        return new MaterialCloudLinkDto(uri.ToString(), DateTime.UtcNow.Add(lifetime));
    }
}
