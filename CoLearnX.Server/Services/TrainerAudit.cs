using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

internal static class TrainerAudit
{
    public static async Task<AuditLog> CreateAsync(
        CoLearnXDbContext db,
        int trainerUserId,
        string action,
        string entityType,
        int entityId,
        string result,
        string? reason,
        CancellationToken ct)
    {
        var staffAccountId = await db.StaffAccounts.AsNoTracking()
            .Where(account => account.UserId == trainerUserId)
            .Select(account => (int?)account.Id)
            .SingleOrDefaultAsync(ct);
        return new AuditLog
        {
            StaffAccountId = staffAccountId,
            UserId = staffAccountId.HasValue ? null : trainerUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Result = result,
            Reason = reason,
        };
    }
}
