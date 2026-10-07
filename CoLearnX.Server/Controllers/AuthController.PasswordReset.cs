using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CoLearnX.Server.Controllers;

public partial class AuthController
{
    [HttpPost("reset-password/status")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPasswordStatus(VerifyEmailRequest request,
        [FromServices] PasswordResetService resets, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { valid = await resets.IsValidAsync(request.Token, ct) });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    public async Task<ActionResult<PasswordResetResponse>> ForgotPassword(
        ForgotPasswordRequest request, [FromServices] PasswordResetService resets, CancellationToken ct)
    {
        await resets.RequestAsync(request.Email, ct);
        return Ok(new PasswordResetResponse(PasswordResetService.RequestMessage));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<PasswordResetResponse>> ResetPassword(
        ResetPasswordRequest request, [FromServices] PasswordResetService resets, CancellationToken ct)
    {
        try
        {
            if (!await resets.ResetAsync(request.Token, request.NewPassword, ct))
                return BadRequest(new ApiError("INVALID_RESET_TOKEN", "This reset link is invalid or expired. Request a new link."));
            return Ok(new PasswordResetResponse("Password updated. Sign in with your new password."));
        }
        catch (FormatException ex)
        {
            return BadRequest(new ApiError("WEAK_PASSWORD", ex.Message));
        }
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("password-reset")]
    public async Task<ActionResult<PasswordResetResponse>> ResendVerification(
        ForgotPasswordRequest request, [FromServices] EmailVerificationService verification, CancellationToken ct)
    {
        await verification.RequestByEmailAsync(request.Email, ct);
        return Ok(new PasswordResetResponse(EmailVerificationService.ResendMessage));
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<ActionResult<PasswordResetResponse>> VerifyEmail(
        VerifyEmailRequest request, [FromServices] EmailVerificationService verification, CancellationToken ct)
    {
        if (!await verification.VerifyAsync(request.Token, ct))
            return BadRequest(new ApiError("INVALID_VERIFICATION_TOKEN", "This verification link is invalid or expired. Request a new link."));
        return Ok(new PasswordResetResponse("Email verified. You can now sign in."));
    }
}
