using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ITrainerLaterPhaseService
{
    Task<IReadOnlyList<MaterialVersionDto>> ListAvailableMaterialsAsync(int trainerUserId, int? courseId = null, CancellationToken ct = default);
    Task<IReadOnlyList<IntakeMaterialDto>> ListMaterialsAsync(int trainerUserId, int intakeId, CancellationToken ct = default);
    Task<IntakeMaterialDto> AttachMaterialAsync(int trainerUserId, int intakeId, AttachIntakeMaterialRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SessionRecordingDto>> ListRecordingsAsync(int trainerUserId, int intakeId, int sessionId, CancellationToken ct = default);
    Task<SessionRecordingDto> AddRecordingAsync(int trainerUserId, int intakeId, int sessionId, CreateSessionRecordingRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceItemDto>> SaveAttendanceAsync(int trainerUserId, int intakeId, int sessionId, SaveAttendanceRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TrainerLearnerDto>> ListLearnersAsync(int trainerUserId, int intakeId, CancellationToken ct = default);
    Task<MaterialFileResult> OpenAttachedMaterialAsync(int trainerUserId, int intakeId, int versionId, CancellationToken ct = default);
    Task<IReadOnlyList<AssessmentDto>> ListAssessmentsAsync(int trainerUserId, int intakeId, CancellationToken ct = default);
    Task<AssessmentDto> CreateAssessmentAsync(int trainerUserId, int intakeId, CreateAssessmentRequest request, CancellationToken ct = default);
    Task<AssessmentResultDto> GradeAsync(int trainerUserId, int assessmentId, int enrollmentId, GradeAssessmentRequest request, CancellationToken ct = default);
    Task<EnrollmentCompletionDto> CompleteAsync(int trainerUserId, int enrollmentId, CancellationToken ct = default);
}

public sealed partial class TrainerLaterPhaseService(CoLearnXDbContext db, IMaterialVersionService materialVersions, IFileStorage files) : ITrainerLaterPhaseService
{
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
