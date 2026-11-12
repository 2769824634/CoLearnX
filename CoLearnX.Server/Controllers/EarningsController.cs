using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[LaterPhaseApiErrors]
public sealed class EarningsController(IEarningsService earnings, IPayoutService payouts) : ControllerBase
{
    [HttpGet("api/earnings/my")]
    [Authorize(Policy = "EarningsRecipient")]
    public async Task<ActionResult<EarningsSummaryDto>> My(CancellationToken ct)
        => Ok(await earnings.GetAsync(User.GetUserId(), ActiveRole(), ct));

    [HttpPost("api/earnings/payouts")]
    [Authorize(Policy = "EarningsRecipient")]
    public async Task<ActionResult<PayoutRequestDto>> RequestPayout([FromBody] CreatePayoutRequest request, CancellationToken ct)
        => Ok(await payouts.RequestAsync(User.GetUserId(), request.PayoutMethod, ct));

    [HttpGet("api/trainer/earnings")]
    [Authorize(Policy = TrainerAuthorization.PolicyName)]
    public async Task<ActionResult<EarningsSummaryDto>> Trainer(CancellationToken ct)
        => Ok(await earnings.GetAsync(User.GetUserId(), AppRole.Trainer, ct));

    [HttpGet("api/creator/earnings")]
    [Authorize(Policy = CreatorAuthorization.PolicyName)]
    public async Task<ActionResult<EarningsSummaryDto>> Creator(CancellationToken ct)
        => Ok(await earnings.GetAsync(User.GetUserId(), AppRole.Creator, ct));

    private AppRole ActiveRole()
        => string.Equals(User.FindFirst("active_role")?.Value, "Creator", StringComparison.OrdinalIgnoreCase)
            ? AppRole.Creator : AppRole.Trainer;
}
