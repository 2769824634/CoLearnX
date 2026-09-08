using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
public class AdminAuditLogsController(IAuditLogService auditLogs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> List(
        [FromQuery] AdminAuditLogQuery query,
        CancellationToken ct = default)
        => Ok(await auditLogs.QueryAsync(query, ct));
}
