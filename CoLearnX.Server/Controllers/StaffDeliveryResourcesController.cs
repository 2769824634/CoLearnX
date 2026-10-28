using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Policy = StaffAuthorization.PolicyName)]
[LaterPhaseApiErrors]
public sealed class StaffDeliveryResourcesController(ITrainerLaterPhaseService trainer) : ControllerBase
{
    [HttpGet("learning-materials")]
    public async Task<ActionResult<IReadOnlyList<MaterialVersionDto>>> AvailableMaterials(
        [FromQuery] int? courseId,
        CancellationToken ct)
        => Ok(await trainer.ListAvailableMaterialsAsync(User.GetUserId(), courseId, ct));

    [HttpGet("intakes/{intakeId:int}/learning-materials")]
    public async Task<ActionResult<IReadOnlyList<IntakeMaterialDto>>> IntakeMaterials(int intakeId, CancellationToken ct)
        => Ok(await trainer.ListMaterialsAsync(User.GetUserId(), intakeId, ct));

    [HttpPost("intakes/{intakeId:int}/learning-materials")]
    public async Task<ActionResult<IntakeMaterialDto>> AttachMaterial(
        int intakeId,
        AttachIntakeMaterialRequest request,
        CancellationToken ct)
        => Ok(await trainer.AttachMaterialAsync(User.GetUserId(), intakeId, request, ct));

    [HttpGet("intakes/{intakeId:int}/learning-materials/{versionId:int}/file")]
    public async Task<IActionResult> DownloadMaterial(int intakeId, int versionId, CancellationToken ct)
    {
        var file = await trainer.OpenAttachedMaterialAsync(User.GetUserId(), intakeId, versionId, ct);
        return File(file.Stream, file.ContentType, file.DownloadName);
    }

    [HttpGet("intakes/{intakeId:int}/sessions/{sessionId:int}/recordings")]
    public async Task<ActionResult<IReadOnlyList<SessionRecordingDto>>> Recordings(
        int intakeId,
        int sessionId,
        CancellationToken ct)
        => Ok(await trainer.ListRecordingsAsync(User.GetUserId(), intakeId, sessionId, ct));

    [HttpPost("intakes/{intakeId:int}/sessions/{sessionId:int}/recordings")]
    public async Task<ActionResult<SessionRecordingDto>> AddRecording(
        int intakeId,
        int sessionId,
        CreateSessionRecordingRequest request,
        CancellationToken ct)
    {
        var result = await trainer.AddRecordingAsync(User.GetUserId(), intakeId, sessionId, request, ct);
        return Created($"/api/staff/intakes/{intakeId}/sessions/{sessionId}/recordings/{result.Id}", result);
    }

    [HttpGet("intakes/{intakeId:int}/sessions/{sessionId:int}/materials")]
    public async Task<ActionResult<IReadOnlyList<SessionMaterialDto>>> SessionMaterials(
        int intakeId,
        int sessionId,
        CancellationToken ct)
        => Ok(await trainer.ListSessionMaterialsAsync(User.GetUserId(), intakeId, sessionId, ct));

    [HttpPost("intakes/{intakeId:int}/sessions/{sessionId:int}/materials")]
    [RequestSizeLimit(MaterialFiles.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<SessionMaterialDto>> UploadSessionMaterial(
        int intakeId,
        int sessionId,
        [FromForm] string? title,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        var result = await trainer.UploadSessionMaterialAsync(User.GetUserId(), intakeId, sessionId, title, file, ct);
        return Created($"/api/staff/intakes/{intakeId}/sessions/{sessionId}/materials/{result.Id}/file", result);
    }

    [HttpGet("intakes/{intakeId:int}/sessions/{sessionId:int}/materials/{materialId:int}/file")]
    public async Task<IActionResult> DownloadSessionMaterial(
        int intakeId,
        int sessionId,
        int materialId,
        CancellationToken ct)
    {
        var file = await trainer.OpenSessionMaterialAsync(User.GetUserId(), intakeId, sessionId, materialId, ct);
        return File(file.Stream, file.ContentType, file.DownloadName);
    }
}
