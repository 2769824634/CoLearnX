using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed class NotificationService(CoLearnXDbContext db)
{
    public async Task<NotificationInboxDto> GetMyAsync(int userId, CancellationToken ct)
    {
        await RequireActiveUserAsync(userId, ct);
        var records = await db.Notifications.AsNoTracking().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToListAsync(ct);
        return new NotificationInboxDto(records.Select(n => new NotificationDto(
            n.Id, n.Code, n.Title, n.Body, n.CreatedAt, n.IsRead, TargetPath(n.Code, n.IntakeId), n.IntakeId)).ToList(),
            records.Count(n => !n.IsRead));
    }

    public async Task MarkReadAsync(int userId, int id, CancellationToken ct)
    {
        await RequireActiveUserAsync(userId, ct);
        // Ownership stays in the UPDATE predicate. Repeating an already-read update is safe.
        var updated = await db.Notifications.Where(n => n.Id == id && n.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), ct);
        if (updated == 0)
            throw new LaterPhaseException("NOTIFICATION_NOT_FOUND", "Notification was not found.", 404);
    }

    public async Task<int> MarkAllReadAsync(int userId, CancellationToken ct)
    {
        await RequireActiveUserAsync(userId, ct);
        return await db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), ct);
    }

    private async Task RequireActiveUserAsync(int userId, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, ct))
            throw new LaterPhaseException("USER_REQUIRED", "An active user account is required.", 403);
    }

    // Links are derived from known event codes, never from notification body or caller input.
    internal static string? TargetPath(string code, int? intakeId = null) => code switch
    {
        "CertificateSubmitted" or "CertificateTrainerApproved" or "CertificateTrainerRejected"
            or "CertificateIssued" or "CertificateAdminRejected" => "/member/badges",
        "N-01" => "/member/programs?tab=reserved",
        "N-class-confirmed" or "N-class-reminder" => "/member/programs?tab=active",
        "N-class-cancelled" or "N-postpone-offer" or "N-postponement-offered" => "/member/programs?tab=history",
        "N-hold-released" or "N-withdraw-70" => "/member/payment",
        "N-session-full" or "N-session-reopened" or "N-min-reached" or "N-under-enrolled" or "N-intake-confirmed" or "N-intake-cancelled"
            => intakeId > 0 ? $"/trainer/courses/intakes/{intakeId}" : "/trainer/courses",
        "N-intake-confirmed-creator" or "N-intake-cancelled-creator"
            => intakeId > 0 ? $"/creator/courses/intake-applications/{intakeId}" : "/creator/courses/intake-applications",
        "N-learner-withdrew" => intakeId > 0 ? $"/trainer/courses/intakes/{intakeId}" : "/trainer/learners",
        "N-topup" => "/member/payment",
        "N-09" => "/member/disputes",
        _ => null,
    };
}
