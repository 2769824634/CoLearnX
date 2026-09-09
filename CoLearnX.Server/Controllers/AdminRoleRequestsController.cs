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
public class AdminRoleRequestsController(IAdminRoleRequestService roleRequests) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminRoleRequestDto>>> List(
        [FromQuery] RoleRequestStatus? status,
        CancellationToken ct)
        => Ok(await roleRequests.ListAsync(status, ct));

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
}
