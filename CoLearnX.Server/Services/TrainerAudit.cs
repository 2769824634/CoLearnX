using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;

namespace CoLearnX.Server.Services;

internal static class TrainerAudit
{
    public static Task<AuditLog> CreateAsync(
        CoLearnXDbContext db,
        int trainerUserId,
        string action,
        string entityType,
        int entityId,
        string result,
        string? reason,
        CancellationToken ct)
        => Task.FromResult(new AuditLog
        {
            UserId = trainerUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Result = result,
            Reason = reason,
        });
}
