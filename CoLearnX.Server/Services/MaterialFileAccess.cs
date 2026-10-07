using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed class MaterialFileAccess(CoLearnXDbContext db, IFileStorage files)
{
    public async Task<(string FilePath, string Title)> RequireCreatorOwnOrApprovedAsync(int userId, int materialId,
        CancellationToken ct = default)
    {
        var material = await db.LearningMaterials.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == materialId && (m.CreatorId == userId || m.Status == MaterialStatus.Approved), ct)
            ?? throw new FileNotFoundException("Material not found.");
        return (material.FilePath, material.Title);
    }

    public async Task<(string FilePath, string Title)> RequireAdminVersionAsync(int versionId, CancellationToken ct = default)
    {
        var version = await db.CourseMaterialVersions.AsNoTracking()
            .Include(item => item.LearningMaterial)
            .FirstOrDefaultAsync(item => item.Id == versionId, ct)
            ?? throw new FileNotFoundException("Material file not found.");
        return (version.FilePath, version.LearningMaterial.Title);
    }

    public async Task<(string FilePath, string Title)> RequireAttachedApprovedAsync(int intakeId, int versionId,
        Exception missing, CancellationToken ct = default)
    {
        var version = await db.CourseIntakeMaterials.AsNoTracking()
            .Where(item => item.CourseIntakeId == intakeId
                && item.CourseMaterialVersionId == versionId
                && item.CourseMaterialVersion.Status == MaterialVersionStatus.Approved)
            .Select(item => new { item.CourseMaterialVersion.FilePath, item.CourseMaterialVersion.LearningMaterial.Title })
            .SingleOrDefaultAsync(ct) ?? throw missing;
        return (version.FilePath, version.Title);
    }

    public async Task<MaterialFileResult> OpenStoredAsync(string filePath, string title, Exception missingFile,
        CancellationToken ct = default)
    {
        var stream = await files.OpenAsync(filePath, ct) ?? throw missingFile;
        return new MaterialFileResult(stream, MaterialFiles.ContentType(Path.GetExtension(filePath)),
            MaterialFiles.DownloadName(title, filePath));
    }
}
