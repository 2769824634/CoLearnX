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
    Task<IReadOnlyList<MemberAssessmentDto>> AssessmentsAsync(int userId, int enrollmentId, CancellationToken ct = default);
    Task<MaterialFileResult> OpenMaterialAsync(int userId, int enrollmentId, int versionId, CancellationToken ct = default);
    Task<MaterialFileResult> OpenSessionMaterialAsync(int userId, int enrollmentId, int materialId, CancellationToken ct = default);
    Task<MaterialFileResult> OpenRecordingAsync(int userId, int enrollmentId, int recordingId, CancellationToken ct = default);
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
            .Where(item => item.CourseSession.CourseIntakeId == enrollment.CourseSession.CourseIntakeId)
            .OrderByDescending(item => item.UploadedAt)
            .Select(item => new MemberHubSessionMaterialDto(item.Id, item.CourseSessionId,
                item.CourseSession.Label, item.Title, item.Format, item.UploadedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MemberHubRecordingDto>> RecordingsAsync(int userId, int enrollmentId, CancellationToken ct = default)
    {
        var enrollment = await RequireEnrollmentAsync(userId, enrollmentId, ct);
        var sessionIds = AccessibleSessionIds(enrollment);
        var recordings = await db.SessionRecordings.AsNoTracking()
            .Where(item => sessionIds.Contains(item.CourseSessionId)
                && item.CourseSession.CourseIntakeId == enrollment.CourseSession.CourseIntakeId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, item.Title, item.RecordingUrl, item.FilePath, item.CreatedAt })
            .ToListAsync(ct);
        return recordings.Select(item => new MemberHubRecordingDto(item.Id, item.Title,
            item.FilePath is null ? item.RecordingUrl : $"/api/enrollments/{enrollmentId}/recordings/{item.Id}/file",
            item.CreatedAt, item.FilePath is not null,
            item.FilePath is null ? null : RecordingFiles.DownloadName(item.Title, item.FilePath))).ToList();
    }

    public async Task<IReadOnlyList<MemberAssessmentDto>> AssessmentsAsync(int userId, int enrollmentId,
        CancellationToken ct = default)
    {
        var enrollment = await RequireEnrollmentAsync(userId, enrollmentId, ct);
        var assessments = await db.Assessments.AsNoTracking()
            .Where(item => item.CourseIntakeId == enrollment.CourseSession.CourseIntakeId)
            .ToListAsync(ct);
        var assessmentIds = assessments.Select(item => item.Id).ToList();
        var results = await db.AssessmentResults.AsNoTracking()
            .Where(item => item.EnrollmentId == enrollmentId && assessmentIds.Contains(item.AssessmentId))
            .ToDictionaryAsync(item => item.AssessmentId, ct);
        var submitted = (await db.AssessmentSubmissionFiles.AsNoTracking()
            .Where(item => item.EnrollmentId == enrollmentId && assessmentIds.Contains(item.AssessmentId))
            .ToListAsync(ct)).ToLookup(item => item.AssessmentId);
        var now = DateTime.UtcNow;
        return assessments
            .OrderBy(item => item.DueAt == null).ThenBy(item => item.DueAt).ThenBy(item => item.CreatedAt)
            .Select(item =>
            {
                var result = results.GetValueOrDefault(item.Id);
                var canSubmit = enrollment.Status == EnrollmentStatus.Active && result is null
                    && !AssessmentSubmissionService.IsPastDue(item.DueAt, now);
                return AssessmentSubmissionService.ToMemberDto(item, result, submitted[item.Id].ToList(), canSubmit);
            })
            .ToList();
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
            .Where(item => item.Id == materialId
                && item.CourseSession.CourseIntakeId == enrollment.CourseSession.CourseIntakeId)
            .Select(item => new { item.FilePath, item.Title })
            .SingleOrDefaultAsync(ct) ?? throw new FileNotFoundException("Session material not found.");
        var stream = await files.OpenAsync(material.FilePath, ct)
            ?? throw new FileNotFoundException("Session material not found.");
        return new MaterialFileResult(stream, MaterialFiles.ContentType(Path.GetExtension(material.FilePath)),
            MaterialFiles.DownloadName(material.Title, material.FilePath));
    }

    public async Task<MaterialFileResult> OpenRecordingAsync(int userId, int enrollmentId, int recordingId,
        CancellationToken ct = default)
    {
        var enrollment = await RequireEnrollmentAsync(userId, enrollmentId, ct);
        var sessionIds = AccessibleSessionIds(enrollment);
        var recording = await db.SessionRecordings.AsNoTracking()
            .Where(item => item.Id == recordingId && sessionIds.Contains(item.CourseSessionId)
                && item.CourseSession.CourseIntakeId == enrollment.CourseSession.CourseIntakeId
                && item.FilePath != null)
            .Select(item => new { item.FilePath, item.Title })
            .SingleOrDefaultAsync(ct) ?? throw new FileNotFoundException("Recording not found.");
        var filePath = recording.FilePath!;
        var stream = await files.OpenAsync(filePath, ct) ?? throw new FileNotFoundException("Recording not found.");
        return new MaterialFileResult(stream, RecordingFiles.ContentType(Path.GetExtension(filePath)),
            RecordingFiles.DownloadName(recording.Title, filePath));
    }

    private async Task<Enrollment> RequireEnrollmentAsync(int userId, int enrollmentId, CancellationToken ct)
        => await db.Enrollments.AsNoTracking().Include(item => item.CourseSession)
            .Include(item => item.DeliveryChoices)
            .FirstOrDefaultAsync(item => item.Id == enrollmentId && item.UserId == userId
                && item.User.IsActive && item.User.Roles.Any(role => role.Role == AppRole.Member)
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed), ct)
            ?? throw new KeyNotFoundException("Enrollment not found.");

    private static int[] AccessibleSessionIds(Enrollment enrollment)
        => enrollment.DeliveryChoices.Count == 0 ? [enrollment.CourseSessionId]
            : enrollment.DeliveryChoices.Select(choice => choice.CourseSessionId).ToArray();
}
