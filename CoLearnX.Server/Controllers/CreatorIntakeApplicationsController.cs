using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/creator/intake-applications")]
[Authorize(Policy = CreatorAuthorization.PolicyName)]
[TrainerApiErrors]
public sealed class CreatorIntakeApplicationsController(ICourseIntakeService intakes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreatorIntakeApplicationSummaryDto>>> List(CancellationToken ct)
        => Ok(await intakes.ListCreatorApplicationsAsync(User.GetUserId(), ct));

    [HttpGet("{courseIntakeId:int}")]
    public async Task<ActionResult<CreatorIntakeApplicationDetailDto>> Get(int courseIntakeId, CancellationToken ct)
        => Ok(await intakes.GetCreatorApplicationAsync(User.GetUserId(), courseIntakeId, ct));

    [HttpPost("{courseIntakeId:int}/review")]
    public async Task<ActionResult<CourseIntakeDetailDto>> Review(int courseIntakeId,
        [FromBody] ReviewIntakeApplicationRequest request, CancellationToken ct)
        => Ok(await intakes.ReviewAsync(User.GetUserId(), courseIntakeId, request, ct));
}
