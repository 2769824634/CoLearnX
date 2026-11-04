using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin/staff")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
public sealed class AdminStaffController(IAdminStaffService staff) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StaffAccountDto>>> List(CancellationToken ct)
        => Ok(await staff.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StaffDetailDto>> Detail(int id, CancellationToken ct)
    {
        var detail = await staff.DetailAsync(id, ct);
        return detail is null ? NotFound(new ApiError("STAFF_NOT_FOUND", "Staff account not found.")) : Ok(detail);
    }

    [HttpGet("{id:int}/document")]
    public async Task<IActionResult> Document(int id, CancellationToken ct)
    {
        var file = await staff.OpenDocumentAsync(id, ct);
        return file is null ? NotFound(new ApiError("NOT_FOUND", "Staff document not found."))
            : File(file.Value.Stream, file.Value.ContentType, file.Value.Name);
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<ActionResult<StaffPasswordResetDto>> ResetPassword(int id, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await staff.ResetPasswordAsync(User.GetAdminAccountId(), id, ct);
        return result is null ? NotFound(new ApiError("STAFF_NOT_FOUND", "Staff account not found.")) : Ok(result);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024 + 64 * 1024)]
    public async Task<ActionResult<StaffProvisioningResultDto>> Provision(
        [FromForm] ProvisionStaffRequest request,
        CancellationToken ct)
    {
        try
        {
            var result = await staff.ProvisionAsync(User.GetAdminAccountId(), request, ct);
            return Created($"/api/admin/staff/{result.Staff.Id}", result);
        }
        catch (StaffAccountException ex)
        {
            return Conflict(new ApiError(ex.Code, ex.Message));
        }
    }

    [HttpPatch("{id:int}/active")]
    public async Task<ActionResult<StaffAccountDto>> SetActive(
        int id,
        [FromBody] SetStaffActiveRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await staff.SetActiveAsync(User.GetAdminAccountId(), id, request.IsActive, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiError("STAFF_NOT_FOUND", ex.Message));
        }
    }
}
