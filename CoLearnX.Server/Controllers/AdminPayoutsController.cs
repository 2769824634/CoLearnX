using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin/payouts")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
[LaterPhaseApiErrors]
public sealed class AdminPayoutsController(IPayoutService payouts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminPayoutDto>>> List([FromQuery] string? status, CancellationToken ct)
    {
        PayoutStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PayoutStatus>(status, true, out var value))
                throw new LaterPhaseException("INVALID_PAYOUT_STATUS", "Unknown payout status.", 400, "status");
            parsed = value;
        }
        return Ok(await payouts.ListAsync(parsed, ct));
    }

    [HttpPost("{id:int}/review")]
    public async Task<ActionResult<AdminPayoutDto>> Review(int id, [FromBody] ReviewPayoutRequest request, CancellationToken ct)
    {
        var raw = request.Decision?.Trim();
        var decision = string.Equals(raw, "Reject", StringComparison.OrdinalIgnoreCase)
            ? PayoutStatus.Rejected
            : Enum.TryParse<PayoutStatus>(raw, true, out var parsed) ? parsed
            : throw new LaterPhaseException("INVALID_PAYOUT_DECISION", "Decision must be Paid or Reject.", 400, "decision");
        return Ok(await payouts.ReviewAsync(User.GetAdminAccountId(), id, decision, request.Reference, ct));
    }
}
