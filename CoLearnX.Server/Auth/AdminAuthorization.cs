using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CoLearnX.Server.Auth;

public static class AdminAuthorization
{
    public const string SchemeName = "AdminBearer";
    public const string PolicyName = "AdminAccount";
    public const string AdminAccountIdClaim = "admin_account_id";
}

public static class AdminClaimsPrincipalExtensions
{
    public static int GetAdminAccountId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(AdminAuthorization.AdminAccountIdClaim)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!int.TryParse(raw, out var id) || id <= 0)
            throw new UnauthorizedAccessException("The administrator identity is invalid.");

        return id;
    }
}
