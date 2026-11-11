using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Data;

// Each account holds one role. Pending requests left over from the old "apply for another role" flow
// can no longer be approved, so they are closed with an explanation.
// Multi-role accounts themselves are cleaned by the demo seed and scripts/single-role-cleanup.sql.
public static class RoleRequestCleanup
{
    public static async Task RejectRequestsForRolesNotHeldAsync(CoLearnXDbContext db, CancellationToken ct = default)
    {
        var stale = await db.RoleRequests
            .Where(request => request.Status == RoleRequestStatus.Pending
                && !db.UserRoles.Any(role => role.UserId == request.UserId && role.Role == request.RequestedRole))
            .ToListAsync(ct);
        foreach (var request in stale)
        {
            request.Status = RoleRequestStatus.Rejected;
            request.ReviewNote = "Each account holds one role. Register a separate account for this role.";
            request.ReviewedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}
