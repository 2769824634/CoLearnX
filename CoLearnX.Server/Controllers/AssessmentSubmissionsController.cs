using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

// A Member hands in documents for an Assessment of their own Intake. Listing stays on GET api/enrollments/{id}/assessments.
[ApiController]
[Route("api/enrollments/{enrollmentId:int}/assessments/{assessmentId:int}/submission")]
[Authorize(Roles = "Member")]
[LaterPhaseApiErrors]
public sealed class MemberAssessmentSubmissionsController(IAssessmentSubmissionService submissions) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "ActiveMember")]
    [RequestSizeLimit(AssignmentFiles.MaxRequestBytes)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<MemberAssessmentDto>> Submit(int enrollmentId, int assessmentId,
        [FromForm] List<IFormFile>? files, CancellationToken ct)
        => Ok(await submissions.SubmitAsync(User.GetUserId(), enrollmentId, assessmentId, files ?? [], ct));

    [HttpGet("files/{fileId:int}")]
    public async Task<IActionResult> Download(int enrollmentId, int assessmentId, int fileId, CancellationToken ct)
    {
        var file = await submissions.OpenOwnFileAsync(User.GetUserId(), enrollmentId, assessmentId, fileId, ct);
        return File(file.Stream, file.ContentType, file.DownloadName);
    }
}

// The Intake's Trainer downloads what learners handed in, next to the grading form.
[ApiController]
[Route("api/trainer/assessments/{assessmentId:int}/submissions")]
[Authorize(Policy = TrainerAuthorization.PolicyName)]
[LaterPhaseApiErrors]
public sealed class TrainerAssessmentSubmissionsController(IAssessmentSubmissionService submissions) : ControllerBase
{
    [HttpGet("files/{fileId:int}")]
    public async Task<IActionResult> Download(int assessmentId, int fileId, CancellationToken ct)
    {
        var file = await submissions.OpenForTrainerAsync(User.GetUserId(), assessmentId, fileId, ct);
        return File(file.Stream, file.ContentType, file.DownloadName);
    }
}
