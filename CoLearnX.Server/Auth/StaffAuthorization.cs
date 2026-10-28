using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CoLearnX.Server.Auth;

public static class StaffAuthorization
{
    public const string SchemeName = "StaffBearer";
    public const string PolicyName = "StaffAccount";
    public const string StaffAccountIdClaim = "staff_account_id";
}

public static class StaffClaimsPrincipalExtensions
{
    public static int GetStaffAccountId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(StaffAuthorization.StaffAccountIdClaim)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!int.TryParse(raw, out var id) || id <= 0)
            throw new UnauthorizedAccessException("The staff identity is invalid.");
        return id;
    }
}
