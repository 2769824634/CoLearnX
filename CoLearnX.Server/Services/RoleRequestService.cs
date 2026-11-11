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
        IReadOnlyList<int>? specialtyIds,
        CancellationToken ct = default);
    Task<IReadOnlyList<SpecialtyOptionDto>> SpecialtyOptionsAsync(CancellationToken ct = default);
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
        var names = await SpecialtyNamesAsync(db, ct);
        return items.Select(item => ToDto(item, names)).ToList();
    }

    public async Task<RoleRequestDto> SubmitAsync(
        int userId,
        string requestedRole,
        IFormFile? resume,
        IFormFile? idDocument,
        string? statement,
        IReadOnlyList<int>? specialtyIds,
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

        if (user.Roles.Any(item => item.Role == AppRole.Member))
            throw new RoleRequestException(
                "ROLE_NOT_REQUESTABLE",
                "Member accounts cannot apply for Trainer or Creator roles.",
                StatusCodes.Status403Forbidden);

        var awaitingCertification = user.Roles.FirstOrDefault(item =>
            item.Role is AppRole.Trainer or AppRole.Creator && item.CertifiedAt == null);
        if (awaitingCertification is not null && awaitingCertification.Role != role)
        {
            throw new RoleRequestException(
                "ROLE_NOT_REQUESTABLE",
                $"Complete certification for your {awaitingCertification.Role} role first.",
                StatusCodes.Status400BadRequest);
        }

        if (!user.Roles.Any(item => item.Role == role))
            throw new RoleRequestException(
                "ROLE_NOT_REQUESTABLE",
                "An account can only request certification for its existing role.",
                StatusCodes.Status403Forbidden);

        if (user.Roles.Any(item => item.Role == role && item.CertifiedAt != null))
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

        // Certification requires 1 to 5 specialties for an existing role.
        var specialties = await ValidateSpecialtiesAsync(specialtyIds, required: awaitingCertification is not null, ct);

        var resumeKey = await SaveRequiredFileAsync(userId, resume, "resume", ct);
        var idKey = await SaveRequiredFileAsync(userId, idDocument, "identity document", ct);

        var entity = new RoleRequest
        {
            UserId = userId,
            RequestedRole = role,
            DegreeOrResumePath = resumeKey,
            IdDocumentPath = idKey,
            ApplicantStatement = note,
            SpecialtyIds = specialties.Count == 0 ? null : string.Join(",", specialties),
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

        return ToDto(entity, await SpecialtyNamesAsync(db, ct));
    }

    public async Task<IReadOnlyList<SpecialtyOptionDto>> SpecialtyOptionsAsync(CancellationToken ct = default)
        => (await db.CourseSubjects.AsNoTracking().OrderBy(item => item.Subject).ThenBy(item => item.SortOrder).ToListAsync(ct))
            .Select(item => new SpecialtyOptionDto(item.Id, item.Name, item.Subject.ToString())).ToList();

    public const int MaxSpecialties = 5;

    private async Task<IReadOnlyList<int>> ValidateSpecialtiesAsync(IReadOnlyList<int>? ids, bool required, CancellationToken ct)
    {
        var chosen = (ids ?? []).Distinct().ToList();
        if (chosen.Count == 0)
        {
            if (!required) return chosen;
            throw new RoleRequestException("SPECIALTIES_REQUIRED", "Choose at least one specialty.", StatusCodes.Status400BadRequest);
        }
        if (chosen.Count > MaxSpecialties)
            throw new RoleRequestException("TOO_MANY_SPECIALTIES", $"Choose at most {MaxSpecialties} specialties.", StatusCodes.Status400BadRequest);
        var known = await db.CourseSubjects.CountAsync(item => chosen.Contains(item.Id), ct);
        if (known != chosen.Count)
            throw new RoleRequestException("SPECIALTY_NOT_FOUND", "One of the chosen specialties does not exist.", StatusCodes.Status400BadRequest);
        return chosen;
    }

    internal static async Task<IReadOnlyDictionary<int, string>> SpecialtyNamesAsync(CoLearnXDbContext db, CancellationToken ct)
        => await db.CourseSubjects.AsNoTracking().ToDictionaryAsync(item => item.Id, item => item.Name, ct);

    internal static IReadOnlyList<int> ParseSpecialtyIds(string? value)
        => string.IsNullOrWhiteSpace(value) ? [] : value.Split(',').Select(int.Parse).ToList();

    internal static IReadOnlyList<string>? SpecialtyNames(string? value, IReadOnlyDictionary<int, string> names)
    {
        var ids = ParseSpecialtyIds(value);
        return ids.Count == 0 ? null : ids.Where(names.ContainsKey).Select(id => names[id]).ToList();
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

    private static RoleRequestDto ToDto(RoleRequest item, IReadOnlyDictionary<int, string> names)
        => new(
            item.Id,
            item.RequestedRole.ToString(),
            item.Status.ToString(),
            item.ReviewNote,
            item.CreatedAt,
            item.ReviewedAt,
            !string.IsNullOrWhiteSpace(item.DegreeOrResumePath),
            !string.IsNullOrWhiteSpace(item.IdDocumentPath),
            item.ApplicantStatement,
            SpecialtyNames(item.SpecialtyIds, names));
}
