using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/trainer")]
[Authorize(Policy = TrainerAuthorization.PolicyName)]
[TrainerApiErrors]
public sealed class TrainerIntakesController(ICourseIntakeService intakes, ITrainerDeliveryService delivery) : ControllerBase
{
    [HttpGet("intakes")]
    public async Task<ActionResult<IReadOnlyList<CourseIntakeSummaryDto>>> List(CancellationToken ct)
        => Ok(await intakes.ListOwnedAsync(User.GetUserId(), ct));

    [HttpGet("intakes/{courseIntakeId:int}")]
    public async Task<ActionResult<CourseIntakeDetailDto>> Get(int courseIntakeId, CancellationToken ct)
        => Ok(await intakes.GetOwnedAsync(User.GetUserId(), courseIntakeId, ct));

    [HttpPatch("intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}/delivery")]
    public async Task<ActionResult<CourseIntakeDetailDto>> UpdateDelivery(int courseIntakeId, int courseSessionId,
        [FromBody] UpdateSessionDeliveryRequest request, CancellationToken ct)
        => Ok(await delivery.UpdateMeetingLinkAsync(User.GetUserId(), courseIntakeId, courseSessionId, request, ct));
}
