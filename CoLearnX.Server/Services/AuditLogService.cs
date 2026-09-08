using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IAuditLogService
{
    Task AppendAdminAsync(
        int adminAccountId,
        string action,
        string entityType,
        string? entityId,
        string result,
        string? reason = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogDto>> ListAsync(int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogDto>> QueryAsync(AdminAuditLogQuery query, CancellationToken ct = default);
}

public class AuditLogService(CoLearnXDbContext db) : IAuditLogService
{
    public async Task AppendAdminAsync(
        int adminAccountId,
        string action,
        string entityType,
        string? entityId,
        string result,
        string? reason = null,
        CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            AdminAccountId = adminAccountId,
            Action = Required(action, nameof(action)),
            EntityType = Required(entityType, nameof(entityType)),
            EntityId = entityId?.Trim(),
            Result = Required(result, nameof(result)),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        });
        await db.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<AuditLogDto>> ListAsync(int limit = 100, CancellationToken ct = default)
        => QueryAsync(new AdminAuditLogQuery { Limit = Math.Clamp(limit, 1, 500) }, ct);

    public async Task<IReadOnlyList<AuditLogDto>> QueryAsync(AdminAuditLogQuery query, CancellationToken ct = default)
    {
        var logs = db.AuditLogs.AsNoTracking();
        if (query.BeforeId.HasValue)
            logs = logs.Where(log => log.Id < query.BeforeId.Value);
        if (!string.IsNullOrWhiteSpace(query.EntityType))
            logs = logs.Where(log => log.EntityType == query.EntityType.Trim());
        if (!string.IsNullOrWhiteSpace(query.Result))
            logs = logs.Where(log => log.Result == query.Result.Trim());
        if (query.ActorType == "Admin")
            logs = logs.Where(log => log.AdminAccountId != null);
        else if (query.ActorType == "User")
            logs = logs.Where(log => log.UserId != null);

        // Append order gives a stable cursor even for equal timestamps or newly arriving logs.
        return await logs
            .OrderByDescending(log => log.Id)
            .Take(Math.Clamp(query.Limit, 1, 500))
            .Select(log => new AuditLogDto(
                log.Id,
                log.AdminAccountId,
                log.UserId,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.Result,
                log.Reason,
                log.CreatedAt))
            .ToListAsync(ct);
    }

    private static string Required(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A value is required.", parameterName)
            : value.Trim();
}
