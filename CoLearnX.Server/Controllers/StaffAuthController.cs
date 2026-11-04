using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/staff/auth")]
public sealed class StaffAuthController(IStaffAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<StaffAuthResponse>> Login(StaffLoginRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await auth.LoginAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiError("STAFF_LOGIN_FAILED", ex.Message));
        }
    }

    [HttpGet("profile")]
    [Authorize(Policy = StaffAuthorization.PolicyName)]
    public async Task<ActionResult<StaffDetailDto>> Profile([FromServices] IAdminStaffService staff, CancellationToken ct)
    {
        var detail = await staff.DetailAsync(User.GetStaffAccountId(), ct);
        return detail is null ? NotFound(new ApiError("STAFF_NOT_FOUND", "Staff account not found.")) : Ok(detail);
    }

    [HttpGet("document")]
    [Authorize(Policy = StaffAuthorization.PolicyName)]
    public async Task<IActionResult> Document([FromServices] IAdminStaffService staff, CancellationToken ct)
    {
        var file = await staff.OpenDocumentAsync(User.GetStaffAccountId(), ct);
        return file is null ? NotFound(new ApiError("NOT_FOUND", "Staff document not found."))
            : File(file.Value.Stream, file.Value.ContentType, file.Value.Name);
    }

    [HttpGet("me")]
    [Authorize(Policy = StaffAuthorization.PolicyName)]
    public async Task<ActionResult<StaffAccountDto>> Me(CancellationToken ct)
    {
        try
        {
            return Ok(await auth.GetMeAsync(User.GetStaffAccountId(), ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiError("STAFF_ACCOUNT_UNAVAILABLE", ex.Message));
        }
    }
}
