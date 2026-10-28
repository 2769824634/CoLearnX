using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/creator")]
[Authorize(Policy = CreatorAuthorization.PolicyName)]
[CourseTrainerApiErrors]
public sealed class CreatorCourseTrainersController(ICourseTrainerService roster) : ControllerBase
{
    [HttpGet("courses/{courseId:int}/trainers")]
    public async Task<ActionResult<IReadOnlyList<CourseTrainerDto>>> List(int courseId, CancellationToken ct)
        => Ok(await roster.ListAsync(User.GetUserId(), courseId, ct));

    [HttpPost("courses/{courseId:int}/trainers")]
    public async Task<ActionResult<CourseTrainerDto>> Invite(int courseId, [FromBody] InviteTrainerRequest request, CancellationToken ct)
        => Ok(await roster.InviteAsync(User.GetUserId(), courseId, request.TrainerId, ct));

    [HttpPost("courses/{courseId:int}/trainers/{trainerId:int}/remove")]
    public async Task<ActionResult<CourseTrainerDto>> Remove(int courseId, int trainerId, CancellationToken ct)
        => Ok(await roster.RemoveAsync(User.GetUserId(), courseId, trainerId, ct));

    [HttpGet("trainers")]
    public async Task<ActionResult<IReadOnlyList<TrainerCandidateDto>>> Candidates([FromQuery] string? search, CancellationToken ct)
        => Ok(await roster.SearchCandidatesAsync(search, ct));
}

[ApiController]
[Route("api/trainer/course-invitations")]
[Authorize(Policy = TrainerAuthorization.PolicyName)]
[CourseTrainerApiErrors]
public sealed class TrainerCourseInvitationsController(ICourseTrainerService roster) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CourseInvitationDto>>> List(CancellationToken ct)
        => Ok(await roster.ListInvitationsAsync(User.GetUserId(), ct));

    [HttpPost("{invitationId:int}/accept")]
    public async Task<ActionResult<CourseInvitationDto>> Accept(int invitationId, CancellationToken ct)
        => Ok(await roster.RespondAsync(User.GetUserId(), invitationId, accept: true, ct));

    [HttpPost("{invitationId:int}/decline")]
    public async Task<ActionResult<CourseInvitationDto>> Decline(int invitationId, CancellationToken ct)
        => Ok(await roster.RespondAsync(User.GetUserId(), invitationId, accept: false, ct));
}
