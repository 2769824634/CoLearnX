using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ICertificateWorkflowService
{
    Task<CertificateRequestDto> SubmitAsync(int userId, SubmitCertificateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CertificateRequestDto>> ListForTrainerAsync(int trainerUserId, string? status, CancellationToken ct = default);
    Task<CertificateRequestDto> TrainerReviewAsync(int trainerUserId, int requestId, WorkflowReviewRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CertificateRequestDto>> ListForAdminAsync(string? status, CancellationToken ct = default);
    Task<CertificateRequestDto> AdminReviewAsync(int adminAccountId, int requestId, WorkflowReviewRequest request, CancellationToken ct = default);
}

public sealed class CertificateWorkflowService(CoLearnXDbContext db) : ICertificateWorkflowService
{
    public async Task<CertificateRequestDto> SubmitAsync(int userId, SubmitCertificateRequest request, CancellationToken ct = default)
    {
        await RequireActiveRoleAsync(userId, AppRole.Member, "MEMBER_REQUIRED", ct);
        var enrollment = await EnrollmentQuery().SingleOrDefaultAsync(item => item.Id == request.EnrollmentId && item.UserId == userId, ct)
            ?? throw new LaterPhaseException("ENROLLMENT_NOT_FOUND", "Enrollment was not found.", 404);
        if (enrollment.Status != EnrollmentStatus.Completed || enrollment.ProgressPercent < 100)
            throw new LaterPhaseException("CERTIFICATE_NOT_ELIGIBLE", "Complete the Intake before requesting a certificate.", 409);

        var intakeId = enrollment.CourseSession.CourseIntakeId;
        var sessionIds = await db.CourseSessions.Where(item => item.CourseIntakeId == intakeId).Select(item => item.Id).ToListAsync(ct);
        var attended = await db.AttendanceRecords.CountAsync(item => sessionIds.Contains(item.CourseSessionId)
            && item.UserId == userId && (item.Status == AttendanceStatus.Present || item.Status == AttendanceStatus.Late), ct);
        if (sessionIds.Count == 0 || attended * 100 / sessionIds.Count < 80)
            throw new LaterPhaseException("CERTIFICATE_NOT_ELIGIBLE", "At least 80% attendance is required.", 409);

        var assessments = await db.Assessments.Where(item => item.CourseIntakeId == intakeId).ToListAsync(ct);
        if (assessments.Count == 0)
            throw new LaterPhaseException("CERTIFICATE_NOT_ELIGIBLE", "At least one assessment is required.", 409);
        var results = await db.AssessmentResults.Where(item => item.EnrollmentId == enrollment.Id
            && assessments.Select(assessment => assessment.Id).Contains(item.AssessmentId)).ToListAsync(ct);
        if (results.Count != assessments.Count || assessments.Any(assessment =>
                results.Single(result => result.AssessmentId == assessment.Id).Score < assessment.PassScore))
            throw new LaterPhaseException("CERTIFICATE_NOT_ELIGIBLE", "All assessments must be graded and passed.", 409);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var existing = await db.CertificateRequests.SingleOrDefaultAsync(item => item.EnrollmentId == enrollment.Id, ct);
        if (existing is not null)
            return await GetDtoAsync(existing.Id, ct);

        var certificateRequest = new CertificateRequest { EnrollmentId = enrollment.Id };
        db.CertificateRequests.Add(certificateRequest);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "CertificateRequestSubmitted",
            EntityType = nameof(CertificateRequest),
            EntityId = certificateRequest.Id.ToString(),
            Result = "Succeeded",
            Reason = enrollment.Course.Code,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetDtoAsync(certificateRequest.Id, ct);
    }

    public async Task<IReadOnlyList<CertificateRequestDto>> ListForTrainerAsync(int trainerUserId, string? status,
        CancellationToken ct = default)
    {
        await RequireActiveRoleAsync(trainerUserId, AppRole.Trainer, "TRAINER_REQUIRED", ct);
        var query = RequestQuery().Where(item => item.Enrollment.CourseSession.CourseIntake.TrainerId == trainerUserId);
        query = FilterStatus(query, status);
        return await query.OrderByDescending(item => item.SubmittedAt).Select(ToDtoExpression()).ToListAsync(ct);
    }

    public async Task<CertificateRequestDto> TrainerReviewAsync(int trainerUserId, int requestId, WorkflowReviewRequest request,
        CancellationToken ct = default)
    {
        await RequireActiveRoleAsync(trainerUserId, AppRole.Trainer, "TRAINER_REQUIRED", ct);
        var item = await RequestQuery().SingleOrDefaultAsync(candidate => candidate.Id == requestId
            && candidate.Enrollment.CourseSession.CourseIntake.TrainerId == trainerUserId, ct)
            ?? throw new LaterPhaseException("CERTIFICATE_REQUEST_NOT_FOUND", "Owned certificate request was not found.", 404);
        var approve = ParseDecision(request.Decision);
        var reason = ReviewReason(request.Reason, approve);
        if (item.Status != CertificateRequestStatus.Submitted)
        {
            var expected = approve ? CertificateRequestStatus.TrainerApproved : CertificateRequestStatus.TrainerRejected;
            if (item.Status != expected || item.TrainerReviewReason != reason)
                throw new LaterPhaseException("CERTIFICATE_REQUEST_ALREADY_REVIEWED", "The certificate request already has a different Trainer decision.", 409);
            return await GetDtoAsync(item.Id, ct);
        }
        item.Status = approve ? CertificateRequestStatus.TrainerApproved : CertificateRequestStatus.TrainerRejected;
        item.TrainerReviewedByUserId = trainerUserId;
        item.TrainerReviewedAt = DateTime.UtcNow;
        item.TrainerReviewReason = reason;
        db.AuditLogs.Add(AuditForUser(trainerUserId, approve ? "CertificateRequestTrainerApproved" : "CertificateRequestTrainerRejected",
            item.Id, reason));
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(item.Id, ct);
    }

    public async Task<IReadOnlyList<CertificateRequestDto>> ListForAdminAsync(string? status, CancellationToken ct = default)
    {
        var query = FilterStatus(RequestQuery(), status);
        return await query.OrderByDescending(item => item.SubmittedAt).Select(ToDtoExpression()).ToListAsync(ct);
    }

    public async Task<CertificateRequestDto> AdminReviewAsync(int adminAccountId, int requestId, WorkflowReviewRequest request,
        CancellationToken ct = default)
    {
        var item = await RequestQuery().SingleOrDefaultAsync(candidate => candidate.Id == requestId, ct)
            ?? throw new LaterPhaseException("CERTIFICATE_REQUEST_NOT_FOUND", "Certificate request was not found.", 404);
        var approve = ParseDecision(request.Decision);
        var reason = ReviewReason(request.Reason, approve);
        if (item.Status is CertificateRequestStatus.Issued or CertificateRequestStatus.AdminRejected)
        {
            var expected = approve ? CertificateRequestStatus.Issued : CertificateRequestStatus.AdminRejected;
            if (item.Status != expected || item.AdminReviewReason != reason)
                throw new LaterPhaseException("CERTIFICATE_REQUEST_ALREADY_REVIEWED", "The certificate request already has a different Admin decision.", 409);
            return await GetDtoAsync(item.Id, ct);
        }
        if (item.Status != CertificateRequestStatus.TrainerApproved)
            throw new LaterPhaseException("TRAINER_REVIEW_REQUIRED", "Trainer approval is required before Admin review.", 409);
        item.AdminReviewedByAccountId = adminAccountId;
        item.AdminReviewedAt = DateTime.UtcNow;
        item.AdminReviewReason = reason;
        if (approve)
        {
            var template = await db.CertificateTemplates.OrderBy(candidate => candidate.StageNumber).FirstOrDefaultAsync(ct)
                ?? throw new LaterPhaseException("CERTIFICATE_TEMPLATE_NOT_FOUND", "No certificate template is configured.", 409);
            var certificate = new UserCertificate
            {
                UserId = item.Enrollment.UserId,
                CertificateTemplateId = template.Id,
                CourseId = item.Enrollment.CourseId,
                VerificationCode = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                AdminApproved = true,
                RequestedByTrainerId = item.TrainerReviewedByUserId,
            };
            db.UserCertificates.Add(certificate);
            item.UserCertificate = certificate;
            item.Status = CertificateRequestStatus.Issued;
        }
        else
        {
            item.Status = CertificateRequestStatus.AdminRejected;
        }
        db.AuditLogs.Add(new AuditLog
        {
            AdminAccountId = adminAccountId,
            Action = approve ? "CertificateIssued" : "CertificateRequestAdminRejected",
            EntityType = nameof(CertificateRequest),
            EntityId = item.Id.ToString(),
            Result = "Succeeded",
            Reason = reason,
        });
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(item.Id, ct);
    }

    private IQueryable<Enrollment> EnrollmentQuery()
        => db.Enrollments.Include(item => item.User).Include(item => item.Course)
            .Include(item => item.CourseSession).ThenInclude(item => item.CourseIntake);

    private IQueryable<CertificateRequest> RequestQuery()
        => db.CertificateRequests.Include(item => item.Enrollment).ThenInclude(item => item.User)
            .Include(item => item.Enrollment).ThenInclude(item => item.Course)
            .Include(item => item.Enrollment).ThenInclude(item => item.CourseSession).ThenInclude(item => item.CourseIntake);

    private async Task<CertificateRequestDto> GetDtoAsync(int requestId, CancellationToken ct)
        => await RequestQuery().Where(item => item.Id == requestId).Select(ToDtoExpression()).SingleAsync(ct);

    private static IQueryable<CertificateRequest> FilterStatus(IQueryable<CertificateRequest> query, string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return query;
        if (!Enum.TryParse<CertificateRequestStatus>(status, true, out var parsed))
            throw new LaterPhaseException("INVALID_STATUS", "Unknown certificate request status.", field: "status");
        return query.Where(item => item.Status == parsed);
    }

    private static System.Linq.Expressions.Expression<Func<CertificateRequest, CertificateRequestDto>> ToDtoExpression()
        => item => new CertificateRequestDto(
            item.Id,
            item.EnrollmentId,
            item.Enrollment.UserId,
            item.Enrollment.User.FullName,
            item.Enrollment.Course.Code,
            item.Enrollment.Course.Title,
            item.Enrollment.CourseSession.CourseIntakeId,
            item.Status.ToString(),
            item.SubmittedAt,
            item.TrainerReviewReason,
            item.AdminReviewReason,
            item.UserCertificateId);

    private static bool ParseDecision(string decision)
        => decision?.Trim().ToUpperInvariant() switch
        {
            "APPROVE" => true,
            "REJECT" => false,
            _ => throw new LaterPhaseException("INVALID_REVIEW_DECISION", "Decision must be Approve or Reject.", field: "decision"),
        };

    private static string? ReviewReason(string? value, bool approve)
    {
        var reason = value?.Trim();
        if (!approve && string.IsNullOrWhiteSpace(reason))
            throw new LaterPhaseException("REVIEW_REASON_REQUIRED", "A reason is required when rejecting a certificate request.", field: "reason");
        return reason;
    }

    private static AuditLog AuditForUser(int userId, string action, int requestId, string? reason)
        => new()
        {
            UserId = userId,
            Action = action,
            EntityType = nameof(CertificateRequest),
            EntityId = requestId.ToString(),
            Result = "Succeeded",
            Reason = reason,
        };

    private async Task RequireActiveRoleAsync(int userId, AppRole role, string code, CancellationToken ct)
    {
        var allowed = await db.Users.AnyAsync(user => user.Id == userId && user.IsActive
            && user.Roles.Any(item => item.Role == role), ct);
        if (!allowed)
            throw new LaterPhaseException(code, $"An active {role} account is required.", 403);
    }
}
