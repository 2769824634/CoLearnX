using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IMaterialVersionService
{
    Task<MaterialVersionDto> CreateAsync(int creatorUserId, CreateMaterialVersionRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MaterialVersionDto>> ListForAdminAsync(string? status, CancellationToken ct = default);
    Task<IReadOnlyList<MaterialVersionDto>> ListApprovedAsync(CancellationToken ct = default);
    Task<MaterialVersionDto> ReviewAsync(int adminAccountId, int versionId, WorkflowReviewRequest request, CancellationToken ct = default);
}

public sealed class MaterialVersionService(CoLearnXDbContext db) : IMaterialVersionService
{
    public async Task<MaterialVersionDto> CreateAsync(int creatorUserId, CreateMaterialVersionRequest request, CancellationToken ct = default)
    {
        var creatorExists = await db.Users.AnyAsync(user => user.Id == creatorUserId && user.IsActive
            && user.Roles.Any(role => role.Role == AppRole.Creator), ct);
        if (!creatorExists)
            throw new LaterPhaseException("CREATOR_REQUIRED", "An active Creator account is required.", 403);

        var title = RequireText(request.Title, 160, "title");
        var filePath = NormalizeMaterialPath(request.FilePath);
        var format = RequireText(request.Format, 32, "format").ToUpperInvariant();
        var category = RequireText(request.Category, 80, "category");
        var material = new LearningMaterial
        {
            CreatorId = creatorUserId,
            Title = title,
            Description = request.Description?.Trim(),
            FilePath = filePath,
            Format = format,
            Category = category,
            Status = MaterialStatus.PendingReview,
            Version = 1,
        };
        var version = new CourseMaterialVersion
        {
            LearningMaterial = material,
            VersionNumber = 1,
            FilePath = filePath,
            Format = format,
            Status = MaterialVersionStatus.PendingApproval,
        };
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.LearningMaterials.Add(material);
        db.CourseMaterialVersions.Add(version);
        await db.SaveChangesAsync(ct);
        db.AuditLogs.Add(new AuditLog
        {
            UserId = creatorUserId,
            Action = "MaterialVersionSubmitted",
            EntityType = nameof(CourseMaterialVersion),
            EntityId = version.Id.ToString(),
            Result = "Succeeded",
            Reason = title,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetAsync(version.Id, ct);
    }

    public async Task<IReadOnlyList<MaterialVersionDto>> ListForAdminAsync(string? status, CancellationToken ct = default)
    {
        var query = db.CourseMaterialVersions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<MaterialVersionStatus>(status, true, out var parsed))
                throw new LaterPhaseException("INVALID_STATUS", "Unknown material version status.", field: "status");
            query = query.Where(item => item.Status == parsed);
        }
        return await Select(query.OrderByDescending(item => item.SubmittedAt)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MaterialVersionDto>> ListApprovedAsync(CancellationToken ct = default)
        => await Select(db.CourseMaterialVersions.AsNoTracking()
            .Where(item => item.Status == MaterialVersionStatus.Approved)
            .OrderBy(item => item.LearningMaterial.Title)).ToListAsync(ct);

    public async Task<MaterialVersionDto> ReviewAsync(int adminAccountId, int versionId, WorkflowReviewRequest request,
        CancellationToken ct = default)
    {
        var version = await db.CourseMaterialVersions.Include(item => item.LearningMaterial)
            .SingleOrDefaultAsync(item => item.Id == versionId, ct)
            ?? throw new LaterPhaseException("MATERIAL_VERSION_NOT_FOUND", "Material version was not found.", 404);
        var decision = ParseDecision(request.Decision);
        var reason = request.Reason?.Trim();
        if (decision == MaterialVersionStatus.Rejected && string.IsNullOrWhiteSpace(reason))
            throw new LaterPhaseException("REVIEW_REASON_REQUIRED", "A reason is required when rejecting a material version.", field: "reason");

        if (version.Status != MaterialVersionStatus.PendingApproval)
        {
            if (version.Status != decision)
                throw new LaterPhaseException("MATERIAL_VERSION_ALREADY_REVIEWED", "The material version already has a different final decision.", 409);
            return (await GetAsync(version.Id, ct)) with { AlreadyReviewed = true };
        }

        version.Status = decision;
        version.ReviewReason = reason;
        version.ReviewedByAdminAccountId = adminAccountId;
        version.ReviewedAt = DateTime.UtcNow;
        version.LearningMaterial.Status = decision == MaterialVersionStatus.Approved ? MaterialStatus.Approved : MaterialStatus.Rejected;
        version.LearningMaterial.RejectionReason = decision == MaterialVersionStatus.Rejected ? reason : null;
        version.LearningMaterial.FilePath = version.FilePath;
        version.LearningMaterial.Format = version.Format;
        version.LearningMaterial.Version = version.VersionNumber;
        db.AuditLogs.Add(new AuditLog
        {
            AdminAccountId = adminAccountId,
            Action = decision == MaterialVersionStatus.Approved ? "MaterialVersionApproved" : "MaterialVersionRejected",
            EntityType = nameof(CourseMaterialVersion),
            EntityId = version.Id.ToString(),
            Result = "Succeeded",
            Reason = reason,
        });
        await db.SaveChangesAsync(ct);
        return await GetAsync(version.Id, ct);
    }

    private async Task<MaterialVersionDto> GetAsync(int versionId, CancellationToken ct)
        => await Select(db.CourseMaterialVersions.AsNoTracking().Where(item => item.Id == versionId)).SingleAsync(ct);

    private static IQueryable<MaterialVersionDto> Select(IQueryable<CourseMaterialVersion> query)
        => query.Select(item => new MaterialVersionDto(
            item.Id,
            item.LearningMaterialId,
            item.LearningMaterial.Title,
            item.LearningMaterial.Description,
            item.FilePath,
            item.Format,
            item.LearningMaterial.Category,
            item.VersionNumber,
            item.Status.ToString(),
            item.LearningMaterial.Creator.FullName,
            item.ReviewReason,
            item.SubmittedAt,
            item.ReviewedAt));

    private static MaterialVersionStatus ParseDecision(string decision)
        => decision?.Trim().ToUpperInvariant() switch
        {
            "APPROVE" => MaterialVersionStatus.Approved,
            "REJECT" => MaterialVersionStatus.Rejected,
            _ => throw new LaterPhaseException("INVALID_REVIEW_DECISION", "Decision must be Approve or Reject.", field: "decision"),
        };

    private static string RequireText(string? value, int maxLength, string field)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result) || result.Length > maxLength)
            throw new LaterPhaseException("INVALID_REQUEST", $"{field} is required and may contain at most {maxLength} characters.", field: field);
        return result;
    }

    private static string NormalizeMaterialPath(string? value)
    {
        var result = RequireText(value, 512, "filePath").Replace('\\', '/');
        if (!result.StartsWith("materials/", StringComparison.OrdinalIgnoreCase)
            || result.Contains("..", StringComparison.Ordinal)
            || result.Contains(':')
            || result.StartsWith('/'))
            throw new LaterPhaseException("INVALID_MATERIAL_PATH", "Use a safe relative path below materials/.", field: "filePath");
        return result;
    }
}
