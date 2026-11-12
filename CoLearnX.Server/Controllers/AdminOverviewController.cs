using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoLearnX.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AdminAuthorization.PolicyName)]
public sealed class AdminOverviewController(IAdminOverviewService overview) : ControllerBase
{
    [HttpGet("classes")]
    public async Task<ActionResult<IReadOnlyList<AdminClassRowDto>>> Classes(CancellationToken ct)
        => Ok(await overview.ListClassesAsync(ct));

    [HttpGet("classes/{classId:int}")]
    public async Task<ActionResult<AdminClassDetailDto>> Class(int classId, CancellationToken ct)
        => await overview.GetClassAsync(classId, ct) is { } detail
            ? Ok(detail)
            : NotFound(new ApiError("CLASS_NOT_FOUND", "Class was not found."));

    [HttpGet("users/{userId:int}/activity")]
    public async Task<ActionResult<AdminUserActivityDto>> UserActivity(int userId, CancellationToken ct)
        => await overview.GetUserActivityAsync(userId, ct) is { } activity
            ? Ok(activity)
            : NotFound(new ApiError("USER_NOT_FOUND", "User was not found."));
}
