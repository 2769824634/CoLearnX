using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
public class AdminController(IAdminFinanceService finance) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserSummaryDto>>> Users(
        [FromQuery] string? search, [FromServices] CoLearnXDbContext db, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking().Include(user => user.Roles).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim().ToLowerInvariant();
            var id = int.TryParse(text, out var parsed) ? parsed : 0;
            query = query.Where(user => user.Id == id || user.Email.ToLower().Contains(text)
                || user.FullName.ToLower().Contains(text));
        }
        var users = await query.OrderBy(user => user.FullName).ThenBy(user => user.Id).Take(100).ToListAsync(ct);
        return Ok(users.Select(user => new AdminUserSummaryDto(user.Id, user.FullName, user.Email,
            user.CreditBalance, user.HeldCredits, user.IsActive, user.Roles.Select(role => role.Role.ToString()).ToList())).ToList());
    }

    [HttpGet("credits/ledger")]
    public async Task<ActionResult<IReadOnlyList<AdminCreditLedgerItemDto>>> Ledger(
        [FromQuery] string? search, [FromQuery] string? type, CancellationToken ct)
        => Ok(await finance.GetLedgerAsync(search, type, ct));
}
