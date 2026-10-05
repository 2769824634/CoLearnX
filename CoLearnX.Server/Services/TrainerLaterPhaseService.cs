using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ITrainerLaterPhaseService
{
    Task<IReadOnlyList<MaterialVersionDto>> ListAvailableMaterialsAsync(int trainerUserId, int? courseId = null, CancellationToken ct = default);
    Task<IReadOnlyList<IntakeMaterialDto>> ListMaterialsAsync(int trainerUserId, int intakeId, CancellationToken ct = default);
    Task<IntakeMaterialDto> AttachMaterialAsync(int trainerUserId, int intakeId, AttachIntakeMaterialRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TrainerReservationDto>> ListReservationsAsync(int trainerUserId, int intakeId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionRecordingDto>> ListRecordingsAsync(int trainerUserId, int intakeId, int sessionId, CancellationToken ct = default);
    Task<SessionRecordingDto> AddRecordingAsync(int trainerUserId, int intakeId, int sessionId, CreateSessionRecordingRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SessionMaterialDto>> ListSessionMaterialsAsync(int trainerUserId, int intakeId, int sessionId, CancellationToken ct = default);
    Task<SessionMaterialDto> UploadSessionMaterialAsync(int trainerUserId, int intakeId, int sessionId, string? title, IFormFile? file, CancellationToken ct = default);
    Task<MaterialFileResult> OpenSessionMaterialAsync(int trainerUserId, int intakeId, int sessionId, int materialId, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceItemDto>> SaveAttendanceAsync(int trainerUserId, int intakeId, int sessionId, SaveAttendanceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TrainerLearnerDto>> ListLearnersAsync(int trainerUserId, int intakeId, CancellationToken ct = default);
    Task<MaterialFileResult> OpenAttachedMaterialAsync(int trainerUserId, int intakeId, int versionId, CancellationToken ct = default);
    Task<IReadOnlyList<AssessmentDto>> ListAssessmentsAsync(int trainerUserId, int intakeId, CancellationToken ct = default);
    Task<AssessmentDto> CreateAssessmentAsync(int trainerUserId, int intakeId, CreateAssessmentRequest request, CancellationToken ct = default);
    Task<AssessmentResultDto> GradeAsync(int trainerUserId, int assessmentId, int enrollmentId, GradeAssessmentRequest request, CancellationToken ct = default);
    Task<EnrollmentCompletionDto> CompleteAsync(int trainerUserId, int enrollmentId, CancellationToken ct = default);
}

public sealed class TrainerLaterPhaseService(CoLearnXDbContext db, IMaterialVersionService materialVersions, IFileStorage files) : ITrainerLaterPhaseService
{
    public async Task<EnrollmentCompletionDto> CompleteAsync(int trainerUserId, int enrollmentId, CancellationToken ct = default)
    {
        var enrollment = await db.Enrollments.Include(e => e.CourseSession)
            .SingleOrDefaultAsync(e => e.Id == enrollmentId, ct)
            ?? throw new LaterPhaseException("ENROLLMENT_NOT_FOUND", "Enrollment was not found.", 404);
        await RequireOwnedIntakeAsync(trainerUserId, enrollment.CourseSession.CourseIntakeId, ct);
        if (enrollment.Status != EnrollmentStatus.Active)
            throw new LaterPhaseException("ENROLLMENT_NOT_COMPLETABLE", "Only an active enrollment can be completed.", 409);
        var rules = await db.Assessments.Where(a => a.CourseIntakeId == enrollment.CourseSession.CourseIntakeId)
            .Select(a => new { a.Id, a.PassScore }).ToListAsync(ct);
        var grades = await db.AssessmentResults.Where(r => r.EnrollmentId == enrollmentId)
            .Select(r => new { r.AssessmentId, r.Score }).ToListAsync(ct);
        if (rules.Any(rule => !grades.Any(g => g.AssessmentId == rule.Id && g.Score >= rule.PassScore)))
            throw new LaterPhaseException("ENROLLMENT_NOT_COMPLETABLE", "All assessments must be passed before completion.", 409);
        enrollment.ProgressPercent = 100;
        enrollment.Status = EnrollmentStatus.Completed;
        enrollment.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new EnrollmentCompletionDto(enrollment.Id, enrollment.Status.ToString(), 100, enrollment.CompletedAt.Value);
    }

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

    public async Task<IReadOnlyList<TrainerReservationDto>> ListReservationsAsync(int trainerUserId, int intakeId,
        CancellationToken ct = default)
    {
        await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        return await db.Enrollments.AsNoTracking()
            .Where(item => item.CourseSession.CourseIntakeId == intakeId && item.Status == EnrollmentStatus.Reserved)
            .OrderBy(item => item.User.FullName).ThenBy(item => item.Id)
            .Select(item => new TrainerReservationDto(item.Id, item.CourseSessionId, item.CourseSession.Label,
                item.User.FullName, item.CreditsSpent, item.EnrolledAt))
            .ToListAsync(ct);
    }

    public async Task<MaterialFileResult> OpenAttachedMaterialAsync(int trainerUserId, int intakeId, int versionId,
        CancellationToken ct = default)
    {
        await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        var version = await db.CourseIntakeMaterials.AsNoTracking()
            .Where(link => link.CourseIntakeId == intakeId
                && link.CourseMaterialVersionId == versionId
                && link.CourseMaterialVersion.Status == MaterialVersionStatus.Approved)
            .Select(link => new { link.CourseMaterialVersion.FilePath, link.CourseMaterialVersion.LearningMaterial.Title })
            .SingleOrDefaultAsync(ct)
            ?? throw new LaterPhaseException("MATERIAL_NOT_FOUND", "The attached material version was not found.", 404);
        var stream = await files.OpenAsync(version.FilePath, ct)
            ?? throw new LaterPhaseException("MATERIAL_NOT_FOUND", "The attached material file was not found.", 404);
        return new MaterialFileResult(stream, MaterialFiles.ContentType(Path.GetExtension(version.FilePath)),
            MaterialFiles.DownloadName(version.Title, version.FilePath));
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

    public async Task<IReadOnlyList<SessionMaterialDto>> ListSessionMaterialsAsync(int trainerUserId, int intakeId,
        int sessionId, CancellationToken ct = default)
    {
        await RequireOwnedSessionAsync(trainerUserId, intakeId, sessionId, ct);
        return await db.SessionMaterials.AsNoTracking()
            .Where(item => item.CourseSessionId == sessionId)
            .OrderByDescending(item => item.UploadedAt)
            .Select(item => new SessionMaterialDto(item.Id, item.CourseSessionId, item.CourseSession.Label,
                item.Title, item.Format, item.UploadedAt))
            .ToListAsync(ct);
    }

    public async Task<SessionMaterialDto> UploadSessionMaterialAsync(int trainerUserId, int intakeId, int sessionId,
        string? title, IFormFile? file, CancellationToken ct = default)
    {
        var session = await RequireOwnedSessionAsync(trainerUserId, intakeId, sessionId, ct);
        RequireDeliveryStatus(session.CourseIntake);

        var cleanTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(cleanTitle) || cleanTitle.Length > 160)
            throw new LaterPhaseException("INVALID_SESSION_MATERIAL", "Title is required and may contain at most 160 characters.", field: "title");
        if (file is null || file.Length <= 0)
            throw new LaterPhaseException("INVALID_SESSION_MATERIAL", "A file is required.", field: "file");
        if (file.Length > MaterialFiles.MaxBytes)
            throw new LaterPhaseException("INVALID_SESSION_MATERIAL", "File must be 20 MB or smaller.", field: "file");

        string extension;
        try
        {
            extension = MaterialFiles.RequireSafeExtension(file.FileName);
        }
        catch (InvalidOperationException ex)
        {
            throw new LaterPhaseException("INVALID_SESSION_MATERIAL", ex.Message, field: "file");
        }

        var key = $"session-materials/{trainerUserId}/{sessionId}/{Guid.NewGuid():N}{extension}";
        try
        {
            await using (var stream = file.OpenReadStream())
                await files.SaveAsync(key, stream, MaterialFiles.ContentType(extension), ct);

            var material = new SessionMaterial
            {
                CourseSessionId = sessionId,
                AddedByTrainerId = trainerUserId,
                Title = cleanTitle,
                FilePath = key,
                Format = MaterialFiles.FormatLabel(extension),
            };
            db.SessionMaterials.Add(material);
            db.AuditLogs.Add(UserAudit(trainerUserId, "SessionMaterialUploaded", nameof(CourseSession), sessionId, cleanTitle));
            await db.SaveChangesAsync(ct);
            return new SessionMaterialDto(material.Id, material.CourseSessionId, session.Label,
                material.Title, material.Format, material.UploadedAt);
        }
        catch
        {
            try { await files.DeleteAsync(key, CancellationToken.None); }
            catch { /* Preserve the original upload or database error. */ }
            throw;
        }
    }

    public async Task<MaterialFileResult> OpenSessionMaterialAsync(int trainerUserId, int intakeId, int sessionId,
        int materialId, CancellationToken ct = default)
    {
        await RequireOwnedSessionAsync(trainerUserId, intakeId, sessionId, ct);
        var material = await db.SessionMaterials.AsNoTracking()
            .Where(item => item.Id == materialId && item.CourseSessionId == sessionId)
            .Select(item => new { item.FilePath, item.Title })
            .SingleOrDefaultAsync(ct)
            ?? throw new LaterPhaseException("SESSION_MATERIAL_NOT_FOUND", "The session material was not found.", 404);
        var stream = await files.OpenAsync(material.FilePath, ct)
            ?? throw new LaterPhaseException("SESSION_MATERIAL_NOT_FOUND", "The session material file was not found.", 404);
        return new MaterialFileResult(stream, MaterialFiles.ContentType(Path.GetExtension(material.FilePath)),
            MaterialFiles.DownloadName(material.Title, material.FilePath));
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

    public async Task<IReadOnlyList<TrainerLearnerDto>> ListLearnersAsync(int trainerUserId, int intakeId, CancellationToken ct = default)
    {
        var intake = await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        var sessionIds = await db.CourseSessions.Where(item => item.CourseIntakeId == intake.Id).Select(item => item.Id).ToListAsync(ct);
        var enrollments = await db.Enrollments.AsNoTracking().Include(item => item.User)
            .Where(item => sessionIds.Contains(item.CourseSessionId)
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed))
            .OrderBy(item => item.User.FullName).ThenBy(item => item.Id).ToListAsync(ct);
        var attendance = await db.AttendanceRecords.AsNoTracking().Where(item => sessionIds.Contains(item.CourseSessionId)).ToListAsync(ct);
        var assessmentRules = await db.Assessments.AsNoTracking().Where(item => item.CourseIntakeId == intake.Id)
            .ToDictionaryAsync(item => item.Id, item => item.PassScore, ct);
        var assessmentIds = assessmentRules.Keys.ToList();
        var results = await db.AssessmentResults.AsNoTracking().Where(item => assessmentIds.Contains(item.AssessmentId)).ToListAsync(ct);
        var requests = await db.CertificateRequests.AsNoTracking().Where(item => enrollments.Select(enrollment => enrollment.Id).Contains(item.EnrollmentId))
            .ToDictionaryAsync(item => item.EnrollmentId, ct);

        return enrollments.Select(enrollment =>
        {
            var learnerAttendance = attendance.Where(item => item.UserId == enrollment.UserId).ToList();
            var attended = learnerAttendance.Count(item => item.Status is AttendanceStatus.Present or AttendanceStatus.Late);
            var rate = sessionIds.Count == 0 ? 0 : attended * 100 / sessionIds.Count;
            var learnerResults = results.Where(item => item.EnrollmentId == enrollment.Id).ToList();
            var passed = learnerResults.Count(result => result.Score >= assessmentRules[result.AssessmentId]);
            var enrollmentAttendance = learnerAttendance.FirstOrDefault(item => item.CourseSessionId == enrollment.CourseSessionId);
            return new TrainerLearnerDto(enrollment.Id, enrollment.CourseSessionId, enrollment.UserId, enrollment.User.FullName, enrollment.User.Email,
                enrollment.Status.ToString(), enrollment.ProgressPercent, rate, learnerResults.Count, passed, enrollmentAttendance?.Status.ToString(),
                requests.GetValueOrDefault(enrollment.Id)?.Status.ToString(), assessmentRules.Count);
        }).ToList();
    }

    public async Task<IReadOnlyList<AssessmentDto>> ListAssessmentsAsync(int trainerUserId, int intakeId, CancellationToken ct = default)
    {
        await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        var items = await db.Assessments.AsNoTracking().Include(item => item.Results).ThenInclude(item => item.Enrollment).ThenInclude(item => item.User)
            .Where(item => item.CourseIntakeId == intakeId).OrderByDescending(item => item.CreatedAt).ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<AssessmentDto> CreateAssessmentAsync(int trainerUserId, int intakeId, CreateAssessmentRequest request,
        CancellationToken ct = default)
    {
        var intake = await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        RequireDeliveryStatus(intake);
        var title = request.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 160)
            throw new LaterPhaseException("INVALID_ASSESSMENT_TITLE", "An assessment title of up to 160 characters is required.", field: "title");
        if (request.MaxScore <= 0 || request.PassScore < 0 || request.PassScore > request.MaxScore)
            throw new LaterPhaseException("INVALID_ASSESSMENT_SCORE", "Pass score must be between zero and the maximum score.", field: "passScore");
        if (request.DueAt.HasValue && request.DueAt.Value.Kind != DateTimeKind.Utc)
            throw new LaterPhaseException("INVALID_UTC_TIME", "Provide a UTC due time with a Z suffix.", field: "dueAt");
        var assessment = new Assessment
        {
            CourseIntakeId = intakeId,
            CreatedByTrainerId = trainerUserId,
            Title = title,
            MaxScore = request.MaxScore,
            PassScore = request.PassScore,
            DueAt = request.DueAt,
        };
        db.Assessments.Add(assessment);
        db.AuditLogs.Add(UserAudit(trainerUserId, "AssessmentCreated", nameof(CourseIntake), intakeId, title));
        await db.SaveChangesAsync(ct);
        return ToDto(assessment);
    }

    public async Task<AssessmentResultDto> GradeAsync(int trainerUserId, int assessmentId, int enrollmentId,
        GradeAssessmentRequest request, CancellationToken ct = default)
    {
        var assessment = await db.Assessments.Include(item => item.CourseIntake)
            .SingleOrDefaultAsync(item => item.Id == assessmentId && item.CourseIntake.TrainerId == trainerUserId, ct)
            ?? throw new LaterPhaseException("ASSESSMENT_NOT_FOUND", "Owned assessment was not found.", 404);
        var enrollment = await db.Enrollments.Include(item => item.User).Include(item => item.CourseSession)
            .SingleOrDefaultAsync(item => item.Id == enrollmentId && item.CourseSession.CourseIntakeId == assessment.CourseIntakeId
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed), ct)
            ?? throw new LaterPhaseException("ENROLLMENT_NOT_FOUND", "Enrollment was not found in this Intake.", 404);
        if (request.Score < 0 || request.Score > assessment.MaxScore)
            throw new LaterPhaseException("INVALID_GRADE", $"Score must be between 0 and {assessment.MaxScore}.", field: "score");
        var result = await db.AssessmentResults.SingleOrDefaultAsync(item => item.AssessmentId == assessmentId && item.EnrollmentId == enrollmentId, ct);
        if (result is null)
        {
            result = new AssessmentResult { AssessmentId = assessmentId, EnrollmentId = enrollmentId };
            db.AssessmentResults.Add(result);
        }
        result.Score = request.Score;
        result.Feedback = request.Feedback?.Trim();
        result.GradedByTrainerId = trainerUserId;
        result.GradedAt = DateTime.UtcNow;
        db.AuditLogs.Add(UserAudit(trainerUserId, "AssessmentGraded", nameof(Assessment), assessmentId,
            $"Enrollment {enrollmentId}: {request.Score}/{assessment.MaxScore}"));
        await db.SaveChangesAsync(ct);
        return new AssessmentResultDto(enrollment.Id, enrollment.UserId, enrollment.User.FullName, result.Score,
            result.Score >= assessment.PassScore, result.Feedback, result.GradedAt);
    }

    private async Task<CourseIntake> RequireOwnedIntakeAsync(int trainerUserId, int intakeId, CancellationToken ct)
    {
        await RequireActiveTrainerAsync(trainerUserId, ct);
        return await db.CourseIntakes.SingleOrDefaultAsync(item => item.Id == intakeId && item.TrainerId == trainerUserId, ct)
            ?? throw new LaterPhaseException("INTAKE_NOT_FOUND", "Owned Intake was not found.", 404);
    }

    private async Task RequireActiveTrainerAsync(int trainerUserId, CancellationToken ct)
    {
        var activeTrainer = await db.Users.AnyAsync(user => user.Id == trainerUserId && user.IsActive
            && user.Roles.Any(role => role.Role == AppRole.Trainer), ct);
        if (!activeTrainer)
            throw new LaterPhaseException("TRAINER_REQUIRED", "An active Trainer account is required.", 403);
    }

    private async Task<CourseSession> RequireOwnedSessionAsync(int trainerUserId, int intakeId, int sessionId, CancellationToken ct)
    {
        await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        return await db.CourseSessions.Include(item => item.CourseIntake)
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.CourseIntakeId == intakeId, ct)
            ?? throw new LaterPhaseException("SESSION_NOT_FOUND", "Session was not found in this Intake.", 404);
    }

    private static void RequireDeliveryStatus(CourseIntake intake)
    {
        if (intake.Status is not (CourseIntakeStatus.Published or CourseIntakeStatus.InProgress))
            throw new LaterPhaseException("DELIVERY_NOT_EDITABLE", "Delivery resources can be changed only for Published or In Progress Intakes.", 409);
    }

    private static SessionRecordingDto ToDto(SessionRecording item)
        => new(item.Id, item.CourseSessionId, item.Title, item.RecordingUrl, item.CreatedAt);

    private static AssessmentDto ToDto(Assessment item)
        => new(item.Id, item.CourseIntakeId, item.Title, item.MaxScore, item.PassScore, item.DueAt, item.CreatedAt,
            item.Results.Select(result => new AssessmentResultDto(result.EnrollmentId, result.Enrollment.UserId,
                result.Enrollment.User.FullName, result.Score, result.Score >= item.PassScore, result.Feedback, result.GradedAt)).ToList());

    private static AuditLog UserAudit(int userId, string action, string entityType, int entityId, string? reason)
        => new()
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Result = "Succeeded",
            Reason = reason,
        };
}
