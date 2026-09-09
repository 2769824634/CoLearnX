using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
public class AdminController(IAdminFinanceService finance) : ControllerBase
{
    [HttpGet("credits/ledger")]
    public async Task<ActionResult<IReadOnlyList<AdminCreditLedgerItemDto>>> Ledger(
        [FromQuery] string? search, [FromQuery] string? type, CancellationToken ct)
        => Ok(await finance.GetLedgerAsync(search, type, ct));
}
