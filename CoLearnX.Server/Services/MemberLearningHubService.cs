using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IMemberLearningHubService
{
    Task<IReadOnlyList<MemberHubMaterialDto>> MaterialsAsync(int userId, int enrollmentId, CancellationToken ct = default);
    Task<IReadOnlyList<MemberHubSessionMaterialDto>> SessionMaterialsAsync(int userId, int enrollmentId, CancellationToken ct = default);
    Task<IReadOnlyList<MemberHubRecordingDto>> RecordingsAsync(int userId, int enrollmentId, CancellationToken ct = default);
    Task<MaterialFileResult> OpenMaterialAsync(int userId, int enrollmentId, int versionId, CancellationToken ct = default);
    Task<MaterialFileResult> OpenSessionMaterialAsync(int userId, int enrollmentId, int materialId, CancellationToken ct = default);
}

public sealed class MemberLearningHubService(CoLearnXDbContext db, IFileStorage files) : IMemberLearningHubService
{
    public async Task<IReadOnlyList<MemberHubMaterialDto>> MaterialsAsync(int userId, int enrollmentId, CancellationToken ct = default)
    {
        var enrollment = await RequireEnrollmentAsync(userId, enrollmentId, ct);
        return await db.CourseIntakeMaterials.AsNoTracking()
            .Where(item => item.CourseIntakeId == enrollment.CourseSession.CourseIntakeId
                && item.CourseMaterialVersion.Status == MaterialVersionStatus.Approved)
            .OrderBy(item => item.CourseMaterialVersion.LearningMaterial.Title)
            .Select(item => new MemberHubMaterialDto(item.CourseMaterialVersionId,
                item.CourseMaterialVersion.LearningMaterial.Title,
                item.CourseMaterialVersion.Format, item.AttachedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MemberHubSessionMaterialDto>> SessionMaterialsAsync(int userId, int enrollmentId,
        CancellationToken ct = default)
    {
        var enrollment = await RequireEnrollmentAsync(userId, enrollmentId, ct);
        return await db.SessionMaterials.AsNoTracking()
            .Where(item => item.CourseSessionId == enrollment.CourseSessionId)
            .OrderByDescending(item => item.UploadedAt)
            .Select(item => new MemberHubSessionMaterialDto(item.Id, item.CourseSessionId,
                item.CourseSession.Label, item.Title, item.Format, item.UploadedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MemberHubRecordingDto>> RecordingsAsync(int userId, int enrollmentId, CancellationToken ct = default)
    {
        var enrollment = await RequireEnrollmentAsync(userId, enrollmentId, ct);
        return await db.SessionRecordings.AsNoTracking()
            .Where(item => item.CourseSessionId == enrollment.CourseSessionId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new MemberHubRecordingDto(item.Id, item.Title, item.RecordingUrl, item.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<MaterialFileResult> OpenMaterialAsync(int userId, int enrollmentId, int versionId, CancellationToken ct = default)
    {
        var enrollment = await RequireEnrollmentAsync(userId, enrollmentId, ct);
        var access = new MaterialFileAccess(db, files);
        var missing = new FileNotFoundException("Material not found.");
        var (filePath, title) = await access.RequireAttachedApprovedAsync(
            enrollment.CourseSession.CourseIntakeId, versionId, missing, ct);
        return await access.OpenStoredAsync(filePath, title, missing, ct);
    }

    public async Task<MaterialFileResult> OpenSessionMaterialAsync(int userId, int enrollmentId, int materialId,
        CancellationToken ct = default)
    {
        var enrollment = await RequireEnrollmentAsync(userId, enrollmentId, ct);
        var material = await db.SessionMaterials.AsNoTracking()
            .Where(item => item.Id == materialId && item.CourseSessionId == enrollment.CourseSessionId)
            .Select(item => new { item.FilePath, item.Title })
            .SingleOrDefaultAsync(ct) ?? throw new FileNotFoundException("Session material not found.");
        var stream = await files.OpenAsync(material.FilePath, ct)
            ?? throw new FileNotFoundException("Session material not found.");
        return new MaterialFileResult(stream, MaterialFiles.ContentType(Path.GetExtension(material.FilePath)),
            MaterialFiles.DownloadName(material.Title, material.FilePath));
    }

    private async Task<Enrollment> RequireEnrollmentAsync(int userId, int enrollmentId, CancellationToken ct)
        => await db.Enrollments.AsNoTracking().Include(item => item.CourseSession)
            .FirstOrDefaultAsync(item => item.Id == enrollmentId && item.UserId == userId
                && item.User.IsActive && item.User.Roles.Any(role => role.Role == AppRole.Member)
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed), ct)
            ?? throw new KeyNotFoundException("Enrollment not found.");
}
