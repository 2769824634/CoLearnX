using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/trainer")]
[Authorize(Policy = TrainerAuthorization.PolicyName)]
[LaterPhaseApiErrors]
public sealed class TrainerLaterPhaseController(
    ITrainerLaterPhaseService trainer,
    ICertificateWorkflowService certificates) : ControllerBase
{
    [HttpGet("learning-materials")]
    public async Task<ActionResult<IReadOnlyList<MaterialVersionDto>>> AvailableMaterials(CancellationToken ct)
        => Ok(await trainer.ListAvailableMaterialsAsync(User.GetUserId(), ct));

    [HttpGet("intakes/{courseIntakeId:int}/learning-materials")]
    public async Task<ActionResult<IReadOnlyList<IntakeMaterialDto>>> IntakeMaterials(int courseIntakeId, CancellationToken ct)
        => Ok(await trainer.ListMaterialsAsync(User.GetUserId(), courseIntakeId, ct));

    [HttpPost("intakes/{courseIntakeId:int}/learning-materials")]
    public async Task<ActionResult<IntakeMaterialDto>> AttachMaterial(int courseIntakeId,
        [FromBody] AttachIntakeMaterialRequest request, CancellationToken ct)
        => Ok(await trainer.AttachMaterialAsync(User.GetUserId(), courseIntakeId, request, ct));

    [HttpGet("intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}/recordings")]
    public async Task<ActionResult<IReadOnlyList<SessionRecordingDto>>> Recordings(int courseIntakeId, int courseSessionId,
        CancellationToken ct)
        => Ok(await trainer.ListRecordingsAsync(User.GetUserId(), courseIntakeId, courseSessionId, ct));

    [HttpPost("intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}/recordings")]
    public async Task<ActionResult<SessionRecordingDto>> AddRecording(int courseIntakeId, int courseSessionId,
        [FromBody] CreateSessionRecordingRequest request, CancellationToken ct)
    {
        var result = await trainer.AddRecordingAsync(User.GetUserId(), courseIntakeId, courseSessionId, request, ct);
        return Created($"/api/trainer/intakes/{courseIntakeId}/sessions/{courseSessionId}/recordings/{result.Id}", result);
    }

    [HttpPut("intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}/attendance")]
    public async Task<ActionResult<IReadOnlyList<AttendanceItemDto>>> SaveAttendance(int courseIntakeId, int courseSessionId,
        [FromBody] SaveAttendanceRequest request, CancellationToken ct)
        => Ok(await trainer.SaveAttendanceAsync(User.GetUserId(), courseIntakeId, courseSessionId, request, ct));

    [HttpGet("intakes/{courseIntakeId:int}/learners")]
    public async Task<ActionResult<IReadOnlyList<TrainerLearnerDto>>> Learners(int courseIntakeId, CancellationToken ct)
        => Ok(await trainer.ListLearnersAsync(User.GetUserId(), courseIntakeId, ct));

    [HttpGet("intakes/{courseIntakeId:int}/assessments")]
    public async Task<ActionResult<IReadOnlyList<AssessmentDto>>> Assessments(int courseIntakeId, CancellationToken ct)
        => Ok(await trainer.ListAssessmentsAsync(User.GetUserId(), courseIntakeId, ct));

    [HttpPost("intakes/{courseIntakeId:int}/assessments")]
    public async Task<ActionResult<AssessmentDto>> CreateAssessment(int courseIntakeId,
        [FromBody] CreateAssessmentRequest request, CancellationToken ct)
    {
        var result = await trainer.CreateAssessmentAsync(User.GetUserId(), courseIntakeId, request, ct);
        return Created($"/api/trainer/intakes/{courseIntakeId}/assessments/{result.Id}", result);
    }

    [HttpPut("assessments/{assessmentId:int}/grades/{enrollmentId:int}")]
    public async Task<ActionResult<AssessmentResultDto>> Grade(int assessmentId, int enrollmentId,
        [FromBody] GradeAssessmentRequest request, CancellationToken ct)
        => Ok(await trainer.GradeAsync(User.GetUserId(), assessmentId, enrollmentId, request, ct));

    [HttpGet("certificate-requests")]
    public async Task<ActionResult<IReadOnlyList<CertificateRequestDto>>> CertificateRequests([FromQuery] string? status,
        CancellationToken ct)
        => Ok(await certificates.ListForTrainerAsync(User.GetUserId(), status, ct));

    [HttpPost("certificate-requests/{certificateRequestId:int}/review")]
    public async Task<ActionResult<CertificateRequestDto>> ReviewCertificate(int certificateRequestId,
        [FromBody] WorkflowReviewRequest request, CancellationToken ct)
        => Ok(await certificates.TrainerReviewAsync(User.GetUserId(), certificateRequestId, request, ct));
}
