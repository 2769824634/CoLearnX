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
    Task<IReadOnlyList<MemberHubRecordingDto>> RecordingsAsync(int userId, int enrollmentId, CancellationToken ct = default);
    Task<MaterialFileResult> OpenMaterialAsync(int userId, int enrollmentId, int versionId, CancellationToken ct = default);
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
        var version = await db.CourseIntakeMaterials.AsNoTracking()
            .Where(item => item.CourseIntakeId == enrollment.CourseSession.CourseIntakeId
                && item.CourseMaterialVersionId == versionId
                && item.CourseMaterialVersion.Status == MaterialVersionStatus.Approved)
            .Select(item => new { item.CourseMaterialVersion.FilePath, item.CourseMaterialVersion.LearningMaterial.Title })
            .SingleOrDefaultAsync(ct) ?? throw new FileNotFoundException("Material not found.");
        var stream = await files.OpenAsync(version.FilePath, ct)
            ?? throw new FileNotFoundException("Material not found.");
        return new MaterialFileResult(stream, MaterialFiles.ContentType(Path.GetExtension(version.FilePath)),
            MaterialFiles.DownloadName(version.Title, version.FilePath));
    }

    private async Task<Enrollment> RequireEnrollmentAsync(int userId, int enrollmentId, CancellationToken ct)
        => await db.Enrollments.AsNoTracking().Include(item => item.CourseSession)
            .FirstOrDefaultAsync(item => item.Id == enrollmentId && item.UserId == userId
                && item.User.IsActive && item.User.Roles.Any(role => role.Role == AppRole.Member)
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed), ct)
            ?? throw new KeyNotFoundException("Enrollment not found.");
}
