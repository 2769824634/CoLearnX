using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public enum RoleRequestDocument
{
    Resume,
    IdDocument,
}

public interface IRoleRequestService
{
    Task<IReadOnlyList<RoleRequestDto>> ListMyAsync(int userId, CancellationToken ct = default);
    Task<RoleRequestDto> SubmitAsync(
        int userId,
        string requestedRole,
        IFormFile? resume,
        IFormFile? idDocument,
        string? statement,
        CancellationToken ct = default);
    Task<MaterialFileResult> OpenDocumentAsync(int roleRequestId, RoleRequestDocument document, CancellationToken ct = default);
}

public sealed class RoleRequestException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed class RoleRequestService(CoLearnXDbContext db, IRoleRequestFileStorage files) : IRoleRequestService
{
    public async Task<IReadOnlyList<RoleRequestDto>> ListMyAsync(int userId, CancellationToken ct = default)
    {
        var items = await db.RoleRequests.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(ct);
        return items.Select(ToDto).ToList();
    }

    public async Task<RoleRequestDto> SubmitAsync(
        int userId,
        string requestedRole,
        IFormFile? resume,
        IFormFile? idDocument,
        string? statement,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<AppRole>(requestedRole, true, out var role)
            || role is not (AppRole.Trainer or AppRole.Creator))
        {
            throw new RoleRequestException(
                "ROLE_NOT_REQUESTABLE",
                "Only Trainer and Creator roles can be requested.",
                StatusCodes.Status400BadRequest);
        }

        var user = await db.Users
            .Include(item => item.Roles)
            .FirstOrDefaultAsync(item => item.Id == userId, ct)
            ?? throw new RoleRequestException("USER_NOT_FOUND", "User was not found.", StatusCodes.Status404NotFound);

        if (user.Roles.Any(item => item.Role == role))
        {
            throw new RoleRequestException(
                "ROLE_ALREADY_GRANTED",
                $"This account already has the {role} role.",
                StatusCodes.Status409Conflict);
        }

        var pending = await db.RoleRequests.AnyAsync(
            item => item.UserId == userId
                && item.RequestedRole == role
                && item.Status == RoleRequestStatus.Pending,
            ct);
        if (pending)
        {
            throw new RoleRequestException(
                "ROLE_REQUEST_PENDING",
                "A pending request for this role already exists.",
                StatusCodes.Status409Conflict);
        }

        var note = ApplicantStatementRules.Normalize(statement);
        if (note.Length == 0)
        {
            throw new RoleRequestException(
                "STATEMENT_REQUIRED",
                "Add a short statement explaining why you are applying.",
                StatusCodes.Status400BadRequest);
        }

        if (note.Length > ApplicantStatementRules.MaxChars
            || ApplicantStatementRules.CountWords(note) > ApplicantStatementRules.MaxWords)
        {
            throw new RoleRequestException(
                "STATEMENT_TOO_LONG",
                "Keep the application statement to 300 words or fewer.",
                StatusCodes.Status400BadRequest);
        }

        var resumeKey = await SaveRequiredFileAsync(userId, resume, "resume", ct);
        var idKey = await SaveRequiredFileAsync(userId, idDocument, "identity document", ct);

        var entity = new RoleRequest
        {
            UserId = userId,
            RequestedRole = role,
            DegreeOrResumePath = resumeKey,
            IdDocumentPath = idKey,
            ApplicantStatement = note,
        };
        db.RoleRequests.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            throw new RoleRequestException(
                "ROLE_REQUEST_PENDING",
                "A pending request for this role already exists.",
                StatusCodes.Status409Conflict);
        }

        return ToDto(entity);
    }

    public async Task<MaterialFileResult> OpenDocumentAsync(
        int roleRequestId,
        RoleRequestDocument document,
        CancellationToken ct = default)
    {
        var item = await db.RoleRequests.AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == roleRequestId, ct)
            ?? throw new FileNotFoundException("Role request was not found.");

        var key = document == RoleRequestDocument.Resume ? item.DegreeOrResumePath : item.IdDocumentPath;
        if (string.IsNullOrWhiteSpace(key))
            throw new FileNotFoundException("Role request file was not found.");

        var stream = await files.OpenAsync(key, ct)
            ?? throw new FileNotFoundException("Role request file was not found.");
        var ext = Path.GetExtension(key);
        var label = document == RoleRequestDocument.Resume ? "resume" : "id-document";
        return new MaterialFileResult(stream, RoleRequestFiles.ContentType(ext), RoleRequestFiles.DownloadName(label, key));
    }

    private async Task<string> SaveRequiredFileAsync(int userId, IFormFile? file, string label, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
        {
            throw new RoleRequestException(
                "FILES_REQUIRED",
                "Upload a resume and an identity document with the application.",
                StatusCodes.Status400BadRequest);
        }

        if (file.Length > RoleRequestFiles.MaxBytes)
        {
            throw new RoleRequestException(
                "FILE_TOO_LARGE",
                $"The {label} must be 10 MB or smaller.",
                StatusCodes.Status400BadRequest);
        }

        string ext;
        try
        {
            ext = RoleRequestFiles.RequireSafeExtension(file.FileName);
        }
        catch (InvalidOperationException ex)
        {
            throw new RoleRequestException("INVALID_FILE_TYPE", ex.Message, StatusCodes.Status400BadRequest);
        }

        var key = $"{userId}/{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        await files.SaveAsync(key, stream, RoleRequestFiles.ContentType(ext), ct);
        return key;
    }

    private static RoleRequestDto ToDto(RoleRequest item)
        => new(
            item.Id,
            item.RequestedRole.ToString(),
            item.Status.ToString(),
            item.ReviewNote,
            item.CreatedAt,
            item.ReviewedAt,
            !string.IsNullOrWhiteSpace(item.DegreeOrResumePath),
            !string.IsNullOrWhiteSpace(item.IdDocumentPath),
            item.ApplicantStatement);
}
