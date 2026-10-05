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
        var intakeIds = records.Where(n => n.IntakeId is > 0)
            .Select(n => n.IntakeId!.Value).Distinct().ToArray();
        var enrollmentIds = records.Where(n => n.EnrollmentId is > 0)
            .Select(n => n.EnrollmentId!.Value).Distinct().ToArray();
        var replacementLinks = await db.CourseIntakes.AsNoTracking()
            .Where(i => intakeIds.Contains(i.Id) && i.ReplacementForIntakeId != null)
            .Select(i => new { i.Id, OriginalIntakeId = i.ReplacementForIntakeId!.Value })
            .ToDictionaryAsync(i => i.Id, ct);
        var enrollmentIntakeIds = intakeIds
            .Concat(replacementLinks.Values.Select(i => i.OriginalIntakeId))
            .Distinct()
            .ToArray();
        var ownedEnrollments = await db.Enrollments.AsNoTracking()
            .Where(e => e.UserId == userId && (enrollmentIds.Contains(e.Id)
                || enrollmentIntakeIds.Contains(e.CourseSession.CourseIntakeId)))
            .OrderByDescending(e => e.EnrolledAt).ThenByDescending(e => e.Id)
            .Select(e => new { e.Id, IntakeId = e.CourseSession.CourseIntakeId })
            .ToListAsync(ct);
        var ownedById = ownedEnrollments.ToDictionary(e => e.Id);
        var fallbackByIntake = ownedEnrollments.GroupBy(e => e.IntakeId)
            .ToDictionary(group => group.Key, group => group.First().Id);
        var items = records.Select(n =>
        {
            var replacementOriginalIntakeId = n.Code is "N-postponement-offered" or "N-postpone-offer"
                && n.IntakeId is > 0
                && replacementLinks.TryGetValue(n.IntakeId.Value, out var replacement)
                    ? replacement.OriginalIntakeId
                    : (int?)null;
            var enrollmentId = n.EnrollmentId is > 0
                && ownedById.TryGetValue(n.EnrollmentId.Value, out var owned)
                && (n.IntakeId is null || owned.IntakeId == n.IntakeId
                    || replacementOriginalIntakeId is > 0 && owned.IntakeId == replacementOriginalIntakeId)
                    ? n.EnrollmentId
                    : replacementOriginalIntakeId is > 0
                        && fallbackByIntake.TryGetValue(replacementOriginalIntakeId.Value, out var replacementFallback)
                            ? replacementFallback
                            : n.IntakeId is > 0 && fallbackByIntake.TryGetValue(n.IntakeId.Value, out var fallback)
                                ? fallback
                                : null;
            return new NotificationDto(n.Id, n.Code, n.Title, n.Body, n.CreatedAt, n.IsRead,
                TargetPath(n.Code, n.IntakeId, enrollmentId), n.IntakeId, enrollmentId);
        }).ToList();
        return new NotificationInboxDto(items,
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
    internal static string? TargetPath(string code, int? intakeId = null, int? enrollmentId = null) => code switch
    {
        "CertificateSubmitted" or "CertificateTrainerApproved" or "CertificateTrainerRejected"
            or "CertificateIssued" or "CertificateAdminRejected" => "/member/badges",
        "N-01" => MemberProgramsPath("reserved", enrollmentId),
        "N-class-confirmed" or "N-class-reminder" => MemberProgramsPath("active", enrollmentId),
        "N-class-cancelled" or "N-postpone-offer" or "N-postponement-offered" => MemberProgramsPath("history", enrollmentId),
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

    private static string MemberProgramsPath(string tab, int? enrollmentId)
        => enrollmentId is > 0
            ? $"/member/programs?tab={tab}&enrollmentId={enrollmentId}"
            : $"/member/programs?tab={tab}";
}
