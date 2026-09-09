using System.ComponentModel.DataAnnotations;
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

    [HttpPost("courses/{courseId:int}/intakes")]
    public async Task<ActionResult<CourseIntakeDetailDto>> Create(int courseId, [FromBody] CreateCourseIntakeRequest request, CancellationToken ct)
    {
        var result = await intakes.CreateAsync(User.GetUserId(), courseId, request, ct);
        return CreatedAtAction(nameof(Get), new { courseIntakeId = result.Id }, result);
    }

    [HttpPut("intakes/{courseIntakeId:int}")]
    public async Task<ActionResult<CourseIntakeDetailDto>> Update(int courseIntakeId, [FromBody] UpdateCourseIntakeRequest request, CancellationToken ct)
        => Ok(await intakes.UpdateAsync(User.GetUserId(), courseIntakeId, request, ct));

    [HttpPost("intakes/{courseIntakeId:int}/sessions")]
    public async Task<ActionResult<CourseIntakeDetailDto>> AddSession(int courseIntakeId, [FromBody] CreateCourseSessionRequest request, CancellationToken ct)
        => Ok(await intakes.AddSessionAsync(User.GetUserId(), courseIntakeId, request, ct));

    [HttpPut("intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}")]
    public async Task<ActionResult<CourseIntakeDetailDto>> UpdateSession(int courseIntakeId, int courseSessionId,
        [FromBody] UpdateCourseSessionRequest request, CancellationToken ct)
        => Ok(await intakes.UpdateSessionAsync(User.GetUserId(), courseIntakeId, courseSessionId, request, ct));

    // Query version avoids a DELETE request body while preserving the aggregate concurrency check.
    [HttpDelete("intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}")]
    public async Task<ActionResult<CourseIntakeDetailDto>> DeleteSession(int courseIntakeId, int courseSessionId,
        [FromQuery, Required] Guid? version, CancellationToken ct)
        => Ok(await intakes.DeleteSessionAsync(User.GetUserId(), courseIntakeId, courseSessionId, version!.Value, ct));

    [HttpPost("intakes/{courseIntakeId:int}/submit")]
    public async Task<ActionResult<CourseIntakeDetailDto>> Submit(int courseIntakeId, [FromBody] SubmitCourseIntakeRequest request, CancellationToken ct)
        => Ok(await intakes.SubmitAsync(User.GetUserId(), courseIntakeId, request.Version, ct));

    [HttpPatch("intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}/delivery")]
    public async Task<ActionResult<CourseIntakeDetailDto>> UpdateDelivery(int courseIntakeId, int courseSessionId,
        [FromBody] UpdateSessionDeliveryRequest request, CancellationToken ct)
        => Ok(await delivery.UpdateMeetingLinkAsync(User.GetUserId(), courseIntakeId, courseSessionId, request, ct));

    [HttpPost("intakes/{courseIntakeId:int}/change-requests")]
    public async Task<ActionResult<CourseIntakeDetailDto>> RequestChange(int courseIntakeId,
        [FromBody] CreateCourseIntakeChangeRequest request, CancellationToken ct)
        => Ok(await intakes.RequestChangeAsync(User.GetUserId(), courseIntakeId, request, ct));
}
