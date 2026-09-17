using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/role-requests")]
[Authorize]
public sealed class RoleRequestsController(IRoleRequestService roleRequests) : ControllerBase
{
    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<RoleRequestDto>>> My(CancellationToken ct)
        => Ok(await roleRequests.ListMyAsync(User.GetUserId(), ct));

    [HttpPost]
    [Consumes("application/json")]
    public ActionResult CreateWithoutFiles()
        => BadRequest(new ApiError(
            "FILES_REQUIRED",
            "Upload a resume and an identity document with the application."));

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RoleRequestFiles.MaxRequestBytes)]
    public async Task<ActionResult<RoleRequestDto>> Create(
        [FromForm] string requestedRole,
        [FromForm] string? statement,
        IFormFile? resume,
        IFormFile? idDocument,
        CancellationToken ct)
    {
        try
        {
            var created = await roleRequests.SubmitAsync(User.GetUserId(), requestedRole, resume, idDocument, statement, ct);
            return Created($"/api/role-requests/{created.Id}", created);
        }
        catch (RoleRequestException ex)
        {
            return StatusCode(ex.StatusCode, new ApiError(ex.Code, ex.Message));
        }
    }
}
