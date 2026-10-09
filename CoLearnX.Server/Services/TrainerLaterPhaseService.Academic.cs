using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed partial class TrainerLaterPhaseService
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

    public async Task<IReadOnlyList<TrainerLearnerDto>> ListLearnersAsync(int trainerUserId, int intakeId, CancellationToken ct = default)
    {
        var intake = await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        var sessionIds = await db.CourseSessions.Where(item => item.CourseIntakeId == intake.Id)
            .OrderBy(item => item.StartsAt).ThenBy(item => item.Id)
            .Select(item => item.Id).ToListAsync(ct);
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
            var sessionAttendances = sessionIds.Select(sessionId =>
            {
                var record = learnerAttendance.FirstOrDefault(item => item.CourseSessionId == sessionId);
                return new TrainerSessionAttendanceDto(sessionId, record?.Status.ToString(), record?.RecordedAt);
            }).ToList();
            return new TrainerLearnerDto(enrollment.Id, enrollment.CourseSessionId, enrollment.UserId, enrollment.User.FullName, enrollment.User.Email,
                enrollment.Status.ToString(), enrollment.ProgressPercent, rate, learnerResults.Count, passed, enrollmentAttendance?.Status.ToString(),
                requests.GetValueOrDefault(enrollment.Id)?.Status.ToString(), assessmentRules.Count, sessionAttendances);
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

    private static AssessmentDto ToDto(Assessment item)
        => new(item.Id, item.CourseIntakeId, item.Title, item.MaxScore, item.PassScore, item.DueAt, item.CreatedAt,
            item.Results.Select(result => new AssessmentResultDto(result.EnrollmentId, result.Enrollment.UserId,
                result.Enrollment.User.FullName, result.Score, result.Score >= item.PassScore, result.Feedback, result.GradedAt)).ToList());

}
