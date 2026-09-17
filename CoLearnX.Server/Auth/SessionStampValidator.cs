using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CoLearnX.Server.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Auth;

public static class SessionStamps
{
    public const string ClaimType = "session_stamp";
    public const string FailureMessage = "This account signed in on another device.";
}

public static class SessionStampValidator
{
    public static async Task ValidateAsync(TokenValidatedContext context, string expectedSubjectType)
    {
        var principal = context.Principal;
        var actualSubjectType = principal?.FindFirst(AuthTokenSubjects.ClaimType)?.Value;
        if (!string.Equals(actualSubjectType, expectedSubjectType, StringComparison.Ordinal))
        {
            context.Fail("Token subject type is not valid for this authentication scheme.");
            return;
        }

        if (principal is null)
            return;

        var db = context.HttpContext.RequestServices.GetRequiredService<CoLearnXDbContext>();
        var tokenStamp = ParseStamp(principal.FindFirstValue(SessionStamps.ClaimType));

        if (string.Equals(expectedSubjectType, AuthTokenSubjects.User, StringComparison.Ordinal))
        {
            var userId = principal.GetUserId();
            if (userId <= 0)
                return;

            var current = await db.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => (Guid?)user.SessionStamp)
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            if (current is null || !Matches(current.Value, tokenStamp))
                context.Fail(SessionStamps.FailureMessage);
            return;
        }

        var adminId = ParseAdminAccountId(principal);
        if (adminId <= 0)
            return;

        var adminStamp = await db.AdminAccounts.AsNoTracking()
            .Where(account => account.Id == adminId)
            .Select(account => (Guid?)account.SessionStamp)
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        // Unknown admin accounts stay 403 via ActiveAdminAccountHandler.
        if (adminStamp is not null && !Matches(adminStamp.Value, tokenStamp))
            context.Fail(SessionStamps.FailureMessage);
    }

    public static Guid NewStamp() => Guid.NewGuid();

    static bool Matches(Guid current, Guid? tokenStamp)
        => current == Guid.Empty || (tokenStamp.HasValue && tokenStamp.Value == current);

    static Guid? ParseStamp(string? raw)
        => Guid.TryParse(raw, out var stamp) ? stamp : null;

    static int ParseAdminAccountId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(AdminAuthorization.AdminAccountIdClaim)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : 0;
    }
}
