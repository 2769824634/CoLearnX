using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
public sealed class EarningsController(EarningsService earnings) : ControllerBase
{
    [HttpGet("api/trainer/earnings")]
    [Authorize(Policy = TrainerAuthorization.PolicyName)]
    public async Task<ActionResult<EarningsSummaryDto>> Trainer(CancellationToken ct)
        => Ok(await earnings.GetAsync(User.GetUserId(), AppRole.Trainer, ct));

    [HttpGet("api/creator/earnings")]
    [Authorize(Policy = CreatorAuthorization.PolicyName)]
    public async Task<ActionResult<EarningsSummaryDto>> Creator(CancellationToken ct)
        => Ok(await earnings.GetAsync(User.GetUserId(), AppRole.Creator, ct));
}
