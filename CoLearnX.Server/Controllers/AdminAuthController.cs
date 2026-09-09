using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin/auth")]
public class AdminAuthController(IAdminAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AdminAuthResponse>> Login(
        [FromBody] AdminLoginRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await auth.LoginAsync(request, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiError("ADMIN_LOGIN_FAILED", ex.Message));
        }
    }

    [HttpGet("me")]
    [Authorize(Policy = AdminAuthorization.PolicyName)]
    public async Task<ActionResult<AdminAccountDto>> Me(CancellationToken ct)
    {
        try
        {
            return Ok(await auth.GetMeAsync(User.GetAdminAccountId(), ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiError("ADMIN_ACCOUNT_UNAVAILABLE", ex.Message));
        }
    }
}
