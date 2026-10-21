using System.ComponentModel.DataAnnotations;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/creator")]
[Authorize(Policy = CreatorAuthorization.PolicyName)]
[TrainerApiErrors]
public sealed class CreatorIntakesController(ICourseIntakeService intakes, IIntakeSettlementService settlement) : ControllerBase
{
    [HttpGet("intakes")]
    public async Task<ActionResult<IReadOnlyList<CourseIntakeSummaryDto>>> List([FromQuery] int? courseId, CancellationToken ct)
        => Ok(await intakes.ListForCreatorAsync(User.GetUserId(), courseId, ct));

    [HttpGet("intakes/{courseIntakeId:int}")]
    public async Task<ActionResult<CourseIntakeDetailDto>> Get(int courseIntakeId, CancellationToken ct)
        => Ok(await intakes.GetForCreatorAsync(User.GetUserId(), courseIntakeId, ct));

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

    [HttpDelete("intakes/{courseIntakeId:int}/sessions/{courseSessionId:int}")]
    public async Task<ActionResult<CourseIntakeDetailDto>> DeleteSession(int courseIntakeId, int courseSessionId,
        [FromQuery, Required] Guid? version, CancellationToken ct)
        => Ok(await intakes.DeleteSessionAsync(User.GetUserId(), courseIntakeId, courseSessionId, version!.Value, ct));

    [HttpPost("intakes/{courseIntakeId:int}/publish")]
    public async Task<ActionResult<CourseIntakeDetailDto>> Publish(int courseIntakeId, [FromBody] SubmitCourseIntakeRequest request, CancellationToken ct)
        => Ok(await intakes.PublishAsync(User.GetUserId(), courseIntakeId, request.Version, ct));

    [HttpPost("intakes/{courseIntakeId:int}/cancel")]
    public async Task<ActionResult<IntakeSettlementResult>> Cancel(int courseIntakeId, CancellationToken ct)
    {
        try { return Ok(await settlement.CancelIntakeAsync(User.GetUserId(), courseIntakeId, ct)); }
        catch (CourseException error) { return StatusCode(error.StatusCode, new ApiError(error.Code, error.Message)); }
    }

    [HttpPost("intakes/{courseIntakeId:int}/postpone")]
    public async Task<ActionResult<CourseIntakeDetailDto>> Postpone(int courseIntakeId, [FromBody] CreateCourseIntakeRequest request, CancellationToken ct)
    {
        var result = await intakes.PostponeAsync(User.GetUserId(), courseIntakeId, request, ct);
        return CreatedAtAction(nameof(Get), new { courseIntakeId = result.Id }, result);
    }
}
