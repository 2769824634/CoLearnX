using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin/learning-paths")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
[LaterPhaseApiErrors]
public sealed class AdminLearningPathsController(LearningPathService paths) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LearningPathDto>>> List(CancellationToken ct)
        => Ok(await paths.GetPathsAsync(null, ct));

    [HttpPut("{id:int}/stages/{number:int}")]
    public async Task<ActionResult<LearningPathDto>> Update(int id, int number, LearningPathStageUpdateRequest request, CancellationToken ct)
        => Ok(await paths.UpdateStageAsync(User.GetAdminAccountId(), id, number, request.RequiredCourseCount, ct));
}
