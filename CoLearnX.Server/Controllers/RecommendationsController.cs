using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Authorize]
[RecommendationApiErrors]
public sealed class RecommendationsController(RecommendationService recommendations) : ControllerBase
{
    [HttpGet("api/interests")]
    public async Task<ActionResult<IReadOnlyList<InterestDto>>> Interests(CancellationToken ct)
        => Ok(await recommendations.InterestTreeAsync(ct));

    [HttpGet("api/users/me/interests")]
    [Authorize(Policy = "ActiveMember")]
    public async Task<ActionResult<MemberInterestsDto>> MyInterests(CancellationToken ct)
        => Ok(await recommendations.GetMemberInterestsAsync(User.GetUserId(), ct));

    [HttpPut("api/users/me/interests")]
    [Authorize(Policy = "ActiveMember")]
    public async Task<ActionResult<MemberInterestsDto>> SaveInterests(UpdateMemberInterestsRequest request, CancellationToken ct)
        => Ok(await recommendations.SaveMemberInterestsAsync(User.GetUserId(), request, ct));

    [HttpGet("api/recommendations")]
    [Authorize(Policy = "ActiveMember")]
    public async Task<ActionResult<RecommendationResponseDto>> Recommendations(CancellationToken ct)
        => Ok(await recommendations.RecommendAsync(User.GetUserId(), ct));
}
