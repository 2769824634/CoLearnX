using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public sealed partial class TrainerLaterPhaseService
{
    public async Task<IReadOnlyList<TrainerReservationDto>> ListReservationsAsync(int trainerUserId, int intakeId,
        CancellationToken ct = default)
    {
        await RequireOwnedIntakeAsync(trainerUserId, intakeId, ct);
        return await db.Enrollments.AsNoTracking()
            .Where(item => item.CourseSession.CourseIntakeId == intakeId && item.Status == EnrollmentStatus.Reserved)
            .OrderBy(item => item.User.FullName).ThenBy(item => item.Id)
            .Select(item => new TrainerReservationDto(item.Id, item.CourseSessionId, item.CourseSession.Label,
                item.User.FullName, item.CreditsSpent, item.EnrolledAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SessionMaterialDto>> ListSessionMaterialsAsync(int trainerUserId, int intakeId,
        int sessionId, CancellationToken ct = default)
    {
        await RequireOwnedSessionAsync(trainerUserId, intakeId, sessionId, ct);
        return await db.SessionMaterials.AsNoTracking()
            .Where(item => item.CourseSessionId == sessionId)
            .OrderByDescending(item => item.UploadedAt)
            .Select(item => new SessionMaterialDto(item.Id, item.CourseSessionId, item.CourseSession.Label,
                item.Title, item.Format, item.UploadedAt))
            .ToListAsync(ct);
    }

    public async Task<SessionMaterialDto> UploadSessionMaterialAsync(int trainerUserId, int intakeId, int sessionId,
        string? title, IFormFile? file, CancellationToken ct = default)
    {
        var session = await RequireOwnedSessionAsync(trainerUserId, intakeId, sessionId, ct);
        RequireDeliveryStatus(session.CourseIntake);

        var cleanTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(cleanTitle) || cleanTitle.Length > 160)
            throw new LaterPhaseException("INVALID_SESSION_MATERIAL", "Title is required and may contain at most 160 characters.", field: "title");
        if (file is null || file.Length <= 0)
            throw new LaterPhaseException("INVALID_SESSION_MATERIAL", "A file is required.", field: "file");
        if (file.Length > MaterialFiles.MaxBytes)
            throw new LaterPhaseException("INVALID_SESSION_MATERIAL", "File must be 20 MB or smaller.", field: "file");

        string extension;
        try
        {
            extension = MaterialFiles.RequireSafeExtension(file.FileName);
        }
        catch (InvalidOperationException ex)
        {
            throw new LaterPhaseException("INVALID_SESSION_MATERIAL", ex.Message, field: "file");
        }

        var key = $"session-materials/{trainerUserId}/{sessionId}/{Guid.NewGuid():N}{extension}";
        try
        {
            await using (var stream = file.OpenReadStream())
                await files.SaveAsync(key, stream, MaterialFiles.ContentType(extension), ct);

            var material = new SessionMaterial
            {
                CourseSessionId = sessionId,
                AddedByTrainerId = trainerUserId,
                Title = cleanTitle,
                FilePath = key,
                Format = MaterialFiles.FormatLabel(extension),
            };
            db.SessionMaterials.Add(material);
            db.AuditLogs.Add(UserAudit(trainerUserId, "SessionMaterialUploaded", nameof(CourseSession), sessionId, cleanTitle));
            await db.SaveChangesAsync(ct);
            return new SessionMaterialDto(material.Id, material.CourseSessionId, session.Label,
                material.Title, material.Format, material.UploadedAt);
        }
        catch
        {
            try { await files.DeleteAsync(key, CancellationToken.None); }
            catch { /* Preserve the original upload or database error. */ }
            throw;
        }
    }

    public async Task<MaterialFileResult> OpenSessionMaterialAsync(int trainerUserId, int intakeId, int sessionId,
        int materialId, CancellationToken ct = default)
    {
        await RequireOwnedSessionAsync(trainerUserId, intakeId, sessionId, ct);
        var material = await db.SessionMaterials.AsNoTracking()
            .Where(item => item.Id == materialId && item.CourseSessionId == sessionId)
            .Select(item => new { item.FilePath, item.Title })
            .SingleOrDefaultAsync(ct)
            ?? throw new LaterPhaseException("SESSION_MATERIAL_NOT_FOUND", "The session material was not found.", 404);
        var stream = await files.OpenAsync(material.FilePath, ct)
            ?? throw new LaterPhaseException("SESSION_MATERIAL_NOT_FOUND", "The session material file was not found.", 404);
        return new MaterialFileResult(stream, MaterialFiles.ContentType(Path.GetExtension(material.FilePath)),
            MaterialFiles.DownloadName(material.Title, material.FilePath));
    }

}
