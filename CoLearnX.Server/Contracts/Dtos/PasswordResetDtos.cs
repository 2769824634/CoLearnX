using System.ComponentModel.DataAnnotations;
using CoLearnX.Server.Auth;

namespace CoLearnX.Server.Contracts.Dtos;

public record ForgotPasswordRequest([Required, EmailAddress, MaxLength(256)] string Email);
public record ResetPasswordRequest(
    [Required, MaxLength(128)] string Token,
    [Required, MinLength(PasswordRules.MinLength), MaxLength(PasswordRules.MaxLength)] string NewPassword);
public record PasswordResetResponse(string Message);
