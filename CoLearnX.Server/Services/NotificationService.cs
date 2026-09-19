using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed class NotificationService(CoLearnXDbContext db)
{
    public async Task<NotificationInboxDto> GetMyAsync(int userId, CancellationToken ct)
    {
        await RequireMemberAsync(userId, ct);
        var records = await db.Notifications.AsNoTracking().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToListAsync(ct);
        return new NotificationInboxDto(records.Select(n => new NotificationDto(
            n.Id, n.Code, n.Title, n.Body, n.CreatedAt, n.IsRead, TargetPath(n.Code))).ToList(),
            records.Count(n => !n.IsRead));
    }

    public async Task MarkReadAsync(int userId, int id, CancellationToken ct)
    {
        await RequireMemberAsync(userId, ct);
        // Ownership stays in the UPDATE predicate. Repeating an already-read update is safe.
        var updated = await db.Notifications.Where(n => n.Id == id && n.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), ct);
        if (updated == 0)
            throw new LaterPhaseException("NOTIFICATION_NOT_FOUND", "Notification was not found.", 404);
    }

    public async Task<int> MarkAllReadAsync(int userId, CancellationToken ct)
    {
        await RequireMemberAsync(userId, ct);
        return await db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), ct);
    }

    private async Task RequireMemberAsync(int userId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive && u.Roles.Any(r => r.Role == AppRole.Member), ct))
            throw new LaterPhaseException("MEMBER_REQUIRED", "An active Member account is required.", 403);
    }

    // Links are derived from known event codes, never from notification body or caller input.
    private static string? TargetPath(string code) => code switch
    {
        "CertificateSubmitted" or "CertificateTrainerApproved" or "CertificateTrainerRejected"
            or "CertificateIssued" or "CertificateAdminRejected" => "/member/badges",
        "N-01" => "/member/programs",
        "N-topup" => "/member/payment",
        "N-09" => "/member/disputes",
        _ => null,
    };
}
