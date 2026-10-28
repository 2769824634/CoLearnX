using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoLearnX.Server.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoLearnX.Server.Auth;

public interface IStaffTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(StaffAccount account);
}

public sealed class StaffTokenService(IOptions<JwtOptions> options) : IStaffTokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(StaffAccount account)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            // Existing delivery services read the User subject as the Trainer ID.
            // The separate staff_account_id claim remains the staff-login identity.
            new(JwtRegisteredClaimNames.Sub, account.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, account.Email),
            new(ClaimTypes.NameIdentifier, account.UserId.ToString()),
            new(ClaimTypes.Name, account.DisplayName),
            new(ClaimTypes.Role, "Trainer"),
            new("active_role", "Trainer"),
            new(StaffAuthorization.StaffAccountIdClaim, account.Id.ToString()),
            new(AuthTokenSubjects.ClaimType, AuthTokenSubjects.Staff),
            new(SessionStamps.ClaimType, account.SessionStamp.ToString("D")),
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
