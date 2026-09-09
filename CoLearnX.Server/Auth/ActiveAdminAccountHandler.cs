using System.Security.Claims;
using CoLearnX.Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Auth;

public sealed class ActiveAdminAccountRequirement : IAuthorizationRequirement
{
    public static ActiveAdminAccountRequirement Instance { get; } = new();

    private ActiveAdminAccountRequirement()
    {
    }
}

public sealed class ActiveAdminAccountHandler(CoLearnXDbContext db)
    : AuthorizationHandler<ActiveAdminAccountRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveAdminAccountRequirement requirement)
    {
        var rawAdminAccountId = context.User.FindFirstValue(AdminAuthorization.AdminAccountIdClaim);
        if (!int.TryParse(rawAdminAccountId, out var adminAccountId) || adminAccountId <= 0)
            return;

        var isActive = await db.AdminAccounts
            .AsNoTracking()
            .AnyAsync(account => account.Id == adminAccountId && account.IsActive);

        if (isActive)
            context.Succeed(requirement);
    }
}
