using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace CoLearnX.Server.Auth;

public static class TrainerAuthorization
{
    public const string PolicyName = "TrainerIntakeAccess";
}

public static class CreatorAuthorization
{
    public const string PolicyName = "CreatorIntakeAccess";
}

// The active role must be certified. Read from the database, never from the token.
public sealed class CertifiedRoleRequirement : IAuthorizationRequirement
{
    public static CertifiedRoleRequirement Instance { get; } = new();
    private CertifiedRoleRequirement() { }
}

public sealed class CertifiedRoleHandler(CoLearnXDbContext db) : AuthorizationHandler<CertifiedRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, CertifiedRoleRequirement requirement)
    {
        var userId = context.User.GetUserId();
        var role = context.User.GetActiveRole();
        var uncertified = await db.UserRoles.AsNoTracking()
            .AnyAsync(item => item.UserId == userId && item.Role == role && item.CertifiedAt == null);
        // Missing roles are left to the role requirements, so only an uncertified role fails here.
        if (!uncertified)
            context.Succeed(requirement);
    }
}

// Only this policy receives the Trainer error envelope; all other authorization stays unchanged.
public sealed class TrainerAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        var endpointPolicies = context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(data => data.Policy).ToHashSet() ?? [];
        var trainerEndpoint = endpointPolicies.Contains(TrainerAuthorization.PolicyName);
        var creatorEndpoint = endpointPolicies.Contains(CreatorAuthorization.PolicyName);
        if ((!trainerEndpoint && !creatorEndpoint) || result.Succeeded)
        {
            await _default.HandleAsync(next, context, policy, result);
            return;
        }
        var failed = result.AuthorizationFailure?.FailedRequirements.ToList() ?? [];
        if (!result.Challenged && failed.Count > 0 && failed.All(item => item is CertifiedRoleRequirement))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ApiError(
                "CERTIFICATION_REQUIRED",
                "Complete identity certification before using this workspace.",
                new Dictionary<string, string[]>()), context.RequestAborted);
            return;
        }
        context.Response.StatusCode = result.Challenged ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
        if (result.Challenged) context.Response.Headers.WWWAuthenticate = "Bearer";
        await context.Response.WriteAsJsonAsync(new ApiError(
            result.Challenged ? "UNAUTHENTICATED" : trainerEndpoint ? "TRAINER_REQUIRED" : "CREATOR_REQUIRED",
            result.Challenged ? "Sign in with a User account to access this operation."
                : trainerEndpoint ? "A Trainer role is required." : "A Creator role is required.",
            new Dictionary<string, string[]>()), context.RequestAborted);
    }
}
