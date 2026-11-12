using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain;
using CoLearnX.Server.Domain.Enums;
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
        var query = db.Users.AsNoTracking().Include(user => user.Roles)
            .Where(user => user.Email != PlatformIdentity.Email);
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

    [HttpGet("users/{id:int}")]
    public async Task<IActionResult> UserDetail(int id, [FromServices] CoLearnXDbContext db, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Roles).Include(u => u.Preference)
            .Include(u => u.TrainerProfile).Include(u => u.CreatorProfile)
            .SingleOrDefaultAsync(u => u.Id == id && u.Email != PlatformIdentity.Email, ct);
        if (user is null) return NotFound(new ApiError("USER_NOT_FOUND", "User was not found."));
        return Ok(new { user.Id, user.Email, user.FullName, user.DisplayName, user.Phone, user.Bio,
            user.Country, user.Occupation, user.CreatedAt, user.EmailVerifiedAt, user.IsActive,
            user.CreditBalance, user.HeldCredits, Roles = user.Roles.Select(r => r.Role.ToString()).ToList(),
            LearningGoals = user.Preference?.LearningGoals, Specialisations = user.TrainerProfile?.Specialisations,
            TrainerHeadline = user.TrainerProfile?.Headline, ExpertiseTags = user.CreatorProfile?.ExpertiseTags,
            CreatorHeadline = user.CreatorProfile?.Headline });
    }

    [HttpGet("pending-counts")]
    public async Task<ActionResult<AdminPendingCountsDto>> PendingCounts([FromServices] CoLearnXDbContext db, CancellationToken ct)
        => Ok(new AdminPendingCountsDto(
            await db.RoleRequests.CountAsync(item => item.Status == RoleRequestStatus.Pending, ct),
            await db.Courses.CountAsync(item => item.Status == CourseStatus.PendingApproval, ct)
                + await AdminCourseReviewService.WithNewMaterials(db).CountAsync(ct),
            await db.CertificateRequests.CountAsync(item => item.Status == CertificateRequestStatus.TrainerApproved, ct),
            await db.Disputes.CountAsync(item => item.Status == DisputeStatus.Open, ct)));

    [HttpGet("credits/ledger")]
    public async Task<ActionResult<IReadOnlyList<AdminCreditLedgerItemDto>>> Ledger(
        [FromQuery] string? search, [FromQuery] string? type, CancellationToken ct, [FromQuery] int? userId = null)
        => Ok(await finance.GetLedgerAsync(search, type, ct, userId));
}
