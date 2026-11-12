using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/trainer")]
[Authorize(Policy = TrainerAuthorization.PolicyName)]
[TrainerApiErrors]
public sealed class TrainerIntakesController(ICourseIntakeService intakes, ITrainerDeliveryService delivery,
    IIntakeInvitationService invitations) : ControllerBase
{
    [HttpGet("intake-invitations")]
    public async Task<ActionResult<IReadOnlyList<IntakeInvitationDto>>> ListInvitations(CancellationToken ct)
        => Ok(await invitations.ListForTrainerAsync(User.GetUserId(), ct));

    [HttpPost("intake-invitations/{courseIntakeId:int}/accept")]
    public async Task<ActionResult<IntakeInvitationDto>> AcceptInvitation(int courseIntakeId, CancellationToken ct)
        => Ok(await invitations.RespondAsync(User.GetUserId(), courseIntakeId, accept: true, reason: null, ct));

    [HttpPost("intake-invitations/{courseIntakeId:int}/decline")]
    public async Task<ActionResult<IntakeInvitationDto>> DeclineInvitation(int courseIntakeId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RespondIntakeInvitationRequest? request, CancellationToken ct)
        => Ok(await invitations.RespondAsync(User.GetUserId(), courseIntakeId, accept: false, request?.Reason, ct));

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
