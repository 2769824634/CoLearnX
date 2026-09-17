using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin/role-requests")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
public class AdminRoleRequestsController(
    IAdminRoleRequestService roleRequests,
    IRoleRequestService applicantRequests) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminRoleRequestDto>>> List(
        [FromQuery] RoleRequestStatus? status,
        CancellationToken ct)
        => Ok(await roleRequests.ListAsync(status, ct));

    [HttpGet("{roleRequestId:int}/resume")]
    public Task<IActionResult> DownloadResume(int roleRequestId, CancellationToken ct)
        => Download(roleRequestId, RoleRequestDocument.Resume, ct);

    [HttpGet("{roleRequestId:int}/id-document")]
    public Task<IActionResult> DownloadIdDocument(int roleRequestId, CancellationToken ct)
        => Download(roleRequestId, RoleRequestDocument.IdDocument, ct);

    [HttpPost("{roleRequestId:int}/review")]
    public async Task<ActionResult<AdminRoleRequestReviewResultDto>> Review(
        int roleRequestId,
        [FromBody] AdminReviewRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await roleRequests.ReviewAsync(
                User.GetAdminAccountId(),
                roleRequestId,
                request,
                ct));
        }
        catch (AdminReviewValidationException ex)
        {
            return BadRequest(new ApiError(ex.Code, ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError("ROLE_REQUEST_NOT_FOUND", ex.Message));
        }
        catch (AdminReviewConflictException ex)
        {
            return Conflict(new ApiError(ex.Code, ex.Message));
        }
    }

    private async Task<IActionResult> Download(int roleRequestId, RoleRequestDocument document, CancellationToken ct)
    {
        try
        {
            var file = await applicantRequests.OpenDocumentAsync(roleRequestId, document, ct);
            return File(file.Stream, file.ContentType, file.DownloadName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new ApiError("NOT_FOUND", "Role request file was not found."));
        }
    }
}
