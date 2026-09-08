using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
public class AdminController(IAdminService admin) : ControllerBase
{
    [HttpGet("credits/ledger")]
    public async Task<ActionResult<IReadOnlyList<CreditLedgerItemDto>>> Ledger(CancellationToken ct)
        => Ok(await admin.GetLedgerAsync(ct));
}
