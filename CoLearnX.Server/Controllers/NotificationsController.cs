using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "Member")]
[LaterPhaseApiErrors]
public class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet("my")]
    public async Task<ActionResult<NotificationInboxDto>> My(CancellationToken ct)
        => Ok(await notifications.GetMyAsync(User.GetUserId(), ct));

    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(User.GetUserId(), id, ct);
        return NoContent();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(User.GetUserId(), ct);
        return NoContent();
    }
}
