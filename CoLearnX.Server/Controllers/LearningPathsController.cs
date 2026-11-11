using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/learning-paths")]
[Authorize(Policy = "ActiveMember")]
public sealed class LearningPathsController(LearningPathService paths) : ControllerBase
{
    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<LearningPathDto>>> My(CancellationToken ct)
        => Ok(await paths.GetPathsAsync(User.GetUserId(), ct));
}
