using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize(Roles = "Member")]
[LaterPhaseApiErrors]
public sealed class DisputesController(IAdminFinanceService finance) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DisputeDto>> Create([FromBody] CreateDisputeRequest request, CancellationToken ct)
    {
        var result = await finance.CreateDisputeAsync(User.GetUserId(), request, ct);
        return Created($"/api/disputes/{result.Id}", result);
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<DisputeDto>>> My(CancellationToken ct)
        => Ok(await finance.ListMyDisputesAsync(User.GetUserId(), ct));
}
