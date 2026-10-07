using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed partial class TrainerLaterPhaseService
{
    public async Task<IReadOnlyList<MaterialVersionDto>> ListAvailableMaterialsAsync(int trainerUserId, int? courseId = null, CancellationToken ct = default)
    {
        await RequireActiveTrainerAsync(trainerUserId, ct);
        return await materialVersions.ListApprovedAsync(courseId, ct);
    }

    public async Task<IReadOnlyList<IntakeMaterialDto>> ListMaterialsAsync(int trainerUserId, int intakeId, CancellationToken ct = default)
    {
        await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        return await db.CourseIntakeMaterials.AsNoTracking().Where(link => link.CourseIntakeId == intakeId)
            .OrderBy(link => link.CourseMaterialVersion.LearningMaterial.Title)
            .Select(link => new IntakeMaterialDto(
                link.CourseMaterialVersionId,
                link.CourseMaterialVersion.LearningMaterialId,
                link.CourseMaterialVersion.LearningMaterial.Title,
                link.CourseMaterialVersion.Format,
                link.CourseMaterialVersion.FilePath,
                link.AttachedAt,
                link.CourseMaterialVersion.VersionNumber))
            .ToListAsync(ct);
    }

    public async Task<IntakeMaterialDto> AttachMaterialAsync(int trainerUserId, int intakeId, AttachIntakeMaterialRequest request,
        CancellationToken ct = default)
    {
        var intake = await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        RequireDeliveryStatus(intake);
        var version = await db.CourseMaterialVersions.Include(item => item.LearningMaterial)
            .SingleOrDefaultAsync(item => item.Id == request.MaterialVersionId, ct)
            ?? throw new LaterPhaseException("MATERIAL_VERSION_NOT_FOUND", "Material version was not found.", 404);
        if (version.Status != MaterialVersionStatus.Approved)
            throw new LaterPhaseException("MATERIAL_NOT_APPROVED", "Only an approved material version can be attached.", 409);
        var belongsToCourse = await db.CourseMaterials.AnyAsync(
            link => link.CourseId == intake.CourseId && link.LearningMaterialId == version.LearningMaterialId, ct);
        if (!belongsToCourse)
            throw new LaterPhaseException("MATERIAL_NOT_FOR_COURSE", "Attach a material that belongs to this Course.", 409);

        var link = await db.CourseIntakeMaterials.SingleOrDefaultAsync(item =>
            item.CourseIntakeId == intakeId && item.CourseMaterialVersionId == version.Id, ct);
        if (link is null)
        {
            link = new CourseIntakeMaterial
            {
                CourseIntakeId = intakeId,
                CourseMaterialVersionId = version.Id,
                AttachedByTrainerId = trainerUserId,
            };
            db.CourseIntakeMaterials.Add(link);
            db.MaterialUsageLogs.Add(new MaterialUsageLog
            {
                LearningMaterialId = version.LearningMaterialId,
                CourseId = intake.CourseId,
                CourseIntakeId = intake.Id,
                TrainerId = trainerUserId,
            });
            db.AuditLogs.Add(UserAudit(trainerUserId, "IntakeMaterialAttached", nameof(CourseIntake), intakeId, version.LearningMaterial.Title));
            await db.SaveChangesAsync(ct);
        }
        return new IntakeMaterialDto(version.Id, version.LearningMaterialId, version.LearningMaterial.Title,
            version.Format, version.FilePath, link.AttachedAt, version.VersionNumber);
    }

    public async Task<MaterialFileResult> OpenAttachedMaterialAsync(int trainerUserId, int intakeId, int versionId,
        CancellationToken ct = default)
    {
        await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        var access = new MaterialFileAccess(db, files);
        var missing = new LaterPhaseException("MATERIAL_NOT_FOUND", "The attached material version was not found.", 404);
        var (filePath, title) = await access.RequireAttachedApprovedAsync(intakeId, versionId, missing, ct);
        return await access.OpenStoredAsync(filePath, title,
            new LaterPhaseException("MATERIAL_NOT_FOUND", "The attached material file was not found.", 404), ct);
    }

    public async Task<IReadOnlyList<SessionRecordingDto>> ListRecordingsAsync(int trainerUserId, int intakeId, int sessionId,
        CancellationToken ct = default)
    {
        await RequireOwnedSessionAsync(trainerUserId, intakeId, sessionId, ct);
        return await db.SessionRecordings.AsNoTracking().Where(item => item.CourseSessionId == sessionId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new SessionRecordingDto(item.Id, item.CourseSessionId, item.Title, item.RecordingUrl, item.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<SessionRecordingDto> AddRecordingAsync(int trainerUserId, int intakeId, int sessionId,
        CreateSessionRecordingRequest request, CancellationToken ct = default)
    {
        var session = await RequireOwnedSessionAsync(trainerUserId, intakeId, sessionId, ct);
        RequireDeliveryStatus(session.CourseIntake);
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 160)
            throw new LaterPhaseException("INVALID_RECORDING_TITLE", "A recording title of up to 160 characters is required.", field: "title");
        if (!Uri.TryCreate(request.RecordingUrl?.Trim(), UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
            throw new LaterPhaseException("INVALID_RECORDING_URL", "Use an absolute HTTP or HTTPS recording URL.", field: "recordingUrl");

        var normalizedUrl = url.ToString();
        var existing = await db.SessionRecordings.SingleOrDefaultAsync(item =>
            item.CourseSessionId == sessionId && item.RecordingUrl == normalizedUrl, ct);
        if (existing is not null)
            return ToDto(existing);

        var recording = new SessionRecording
        {
            CourseSessionId = sessionId,
            AddedByTrainerId = trainerUserId,
            Title = title,
            RecordingUrl = normalizedUrl,
        };
        db.SessionRecordings.Add(recording);
        db.AuditLogs.Add(UserAudit(trainerUserId, "SessionRecordingAdded", nameof(CourseSession), sessionId, title));
        await db.SaveChangesAsync(ct);
        return ToDto(recording);
    }

    public async Task<IReadOnlyList<AttendanceItemDto>> SaveAttendanceAsync(int trainerUserId, int intakeId, int sessionId,
        SaveAttendanceRequest request, CancellationToken ct = default)
    {
        var session = await RequireOwnedSessionAsync(trainerUserId, intakeId, sessionId, ct);
        if (session.CourseIntake.Status is not (CourseIntakeStatus.Published or CourseIntakeStatus.InProgress or CourseIntakeStatus.Completed))
            throw new LaterPhaseException("ATTENDANCE_NOT_EDITABLE", "Attendance can be recorded for Published, In Progress or Completed Intakes.", 409);
        if (request.Records is null || request.Records.Count == 0)
            throw new LaterPhaseException("ATTENDANCE_REQUIRED", "Provide at least one attendance record.", field: "records");
        if (request.Records.Select(item => item.EnrollmentId).Distinct().Count() != request.Records.Count)
            throw new LaterPhaseException("DUPLICATE_ENROLLMENT", "Each enrollment may appear only once.", field: "records");

        var enrollmentIds = request.Records.Select(item => item.EnrollmentId).ToArray();
        var enrollments = await db.Enrollments.Include(item => item.User)
            .Where(item => enrollmentIds.Contains(item.Id) && item.CourseSessionId == sessionId
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed))
            .ToDictionaryAsync(item => item.Id, ct);
        if (enrollments.Count != enrollmentIds.Length)
            throw new LaterPhaseException("ENROLLMENT_NOT_FOUND", "Every attendance row must belong to this Session.", 404, "records");
        if (enrollments.Values.Select(item => item.UserId).Distinct().Count() != enrollments.Count)
            throw new LaterPhaseException("DUPLICATE_LEARNER", "A learner may have only one attendance row per Session.", 409, "records");

        var userIds = enrollments.Values.Select(item => item.UserId).ToArray();
        var existing = await db.AttendanceRecords.Where(item => item.CourseSessionId == sessionId && userIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, ct);
        var now = DateTime.UtcNow;
        foreach (var row in request.Records)
        {
            if (!Enum.TryParse<AttendanceStatus>(row.Status, true, out var status))
                throw new LaterPhaseException("INVALID_ATTENDANCE_STATUS", "Attendance status must be Present, Absent or Late.", field: "status");
            var enrollment = enrollments[row.EnrollmentId];
            if (!existing.TryGetValue(enrollment.UserId, out var record))
            {
                record = new AttendanceRecord { CourseSessionId = sessionId, UserId = enrollment.UserId };
                db.AttendanceRecords.Add(record);
                existing[enrollment.UserId] = record;
            }
            record.Status = status;
            record.RecordedAt = now;
            record.RecordedByTrainerId = trainerUserId;
        }
        db.AuditLogs.Add(UserAudit(trainerUserId, "AttendanceRecorded", nameof(CourseSession), sessionId,
            $"{request.Records.Count} learner(s)"));
        await db.SaveChangesAsync(ct);
        return request.Records.Select(row =>
        {
            var enrollment = enrollments[row.EnrollmentId];
            var record = existing[enrollment.UserId];
            return new AttendanceItemDto(enrollment.Id, enrollment.UserId, enrollment.User.FullName,
                record.Status.ToString(), record.RecordedAt);
        }).ToList();
    }

    private static SessionRecordingDto ToDto(SessionRecording item)
        => new(item.Id, item.CourseSessionId, item.Title, item.RecordingUrl, item.CreatedAt);

}
