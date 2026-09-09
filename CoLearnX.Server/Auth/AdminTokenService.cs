using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoLearnX.Server.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoLearnX.Server.Auth;

public interface IAdminTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(AdminAccount adminAccount);
}

public class AdminTokenService(IOptions<JwtOptions> options) : IAdminTokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(AdminAccount adminAccount)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);
        var id = adminAccount.Id.ToString();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, id),
            new(JwtRegisteredClaimNames.Email, adminAccount.Email),
            new(ClaimTypes.NameIdentifier, id),
            new(ClaimTypes.Name, adminAccount.Email),
            new(AdminAuthorization.AdminAccountIdClaim, id),
            new(AuthTokenSubjects.ClaimType, AuthTokenSubjects.Admin),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
