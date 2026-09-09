using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoLearnX.Server.Auth;

// Issues JWT with active_role claim. Shared: IJwtTokenService, JwtTokenService
public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user, AppRole activeRole, IEnumerable<AppRole> roles);
}

public class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(User user, AppRole activeRole, IEnumerable<AppRole> roles)
    {
        var expires = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(AuthTokenSubjects.ClaimType, AuthTokenSubjects.User),
            new("active_role", activeRole.ToString()),
        };

        foreach (var role in roles.Distinct())
        {
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public static class AuthTokenSubjects
{
    public const string ClaimType = "subject_type";
    public const string User = "user";
    public const string Admin = "admin";
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(raw, out var id) ? id : 0;
    }

    public static AppRole GetActiveRole(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue("active_role") ?? AppRole.Member.ToString();
        return Enum.TryParse<AppRole>(raw, true, out var role) ? role : AppRole.Member;
    }
}
