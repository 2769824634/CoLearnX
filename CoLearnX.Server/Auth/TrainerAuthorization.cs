using CoLearnX.Server.Contracts.Dtos;
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
        context.Response.StatusCode = result.Challenged ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
        if (result.Challenged) context.Response.Headers.WWWAuthenticate = "Bearer";
        await context.Response.WriteAsJsonAsync(new ApiError(
            result.Challenged ? "UNAUTHENTICATED" : trainerEndpoint ? "TRAINER_REQUIRED" : "CREATOR_REQUIRED",
            result.Challenged ? "Sign in with a User account to access this operation."
                : trainerEndpoint ? "A Trainer role is required." : "A Creator role is required.",
            new Dictionary<string, string[]>()), context.RequestAborted);
    }
}
