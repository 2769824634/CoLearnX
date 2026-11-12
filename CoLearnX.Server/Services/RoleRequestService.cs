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
        IReadOnlyList<IFormFile>? resume,
        IReadOnlyList<IFormFile>? idDocument,
        string? statement,
        IReadOnlyList<int>? specialtyIds,
        CancellationToken ct = default);
    Task<IReadOnlyList<SpecialtyOptionDto>> SpecialtyOptionsAsync(CancellationToken ct = default);
    Task<MaterialFileResult> OpenDocumentAsync(int roleRequestId, RoleRequestDocument document, int index = 0, CancellationToken ct = default);
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
        IReadOnlyList<IFormFile>? resume,
        IReadOnlyList<IFormFile>? idDocument,
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

        // Check every file before saving any, so a rejected upload leaves nothing behind.
        var resumeFiles = RequireFiles(resume);
        var idFiles = RequireFiles(idDocument);
        var resumeExts = ValidateFiles(resumeFiles, "resume");
        var idExts = ValidateFiles(idFiles, "identity document");
        if (resumeFiles.Concat(idFiles).Sum(file => file.Length) > RoleRequestFiles.MaxTotalBytes)
        {
            throw new RoleRequestException(
                "FILES_TOO_LARGE",
                $"All files together must be {RoleRequestFiles.MaxTotalBytes / (1024 * 1024)} MB or smaller.",
                StatusCodes.Status400BadRequest);
        }

        var resumeKey = await SaveFilesAsync(userId, resumeFiles, resumeExts, ct);
        var idKey = await SaveFilesAsync(userId, idFiles, idExts, ct);

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
        int index = 0,
        CancellationToken ct = default)
    {
        var item = await db.RoleRequests.AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == roleRequestId, ct)
            ?? throw new FileNotFoundException("Role request was not found.");

        var keys = RoleRequestFiles.SplitKeys(document == RoleRequestDocument.Resume ? item.DegreeOrResumePath : item.IdDocumentPath);
        if (index < 0 || index >= keys.Count)
            throw new FileNotFoundException("Role request file was not found.");
        var key = keys[index];

        var stream = await files.OpenAsync(key, ct)
            ?? throw new FileNotFoundException("Role request file was not found.");
        var ext = Path.GetExtension(key);
        var label = document == RoleRequestDocument.Resume ? "resume" : "id-document";
        if (keys.Count > 1) label += $"-{index + 1}";
        return new MaterialFileResult(stream, RoleRequestFiles.ContentType(ext), RoleRequestFiles.DownloadName(label, key));
    }

    private static IReadOnlyList<IFormFile> RequireFiles(IReadOnlyList<IFormFile>? files)
    {
        if (files is null || files.Count == 0 || files.Any(file => file.Length <= 0))
        {
            throw new RoleRequestException(
                "FILES_REQUIRED",
                "Upload a resume and an identity document with the application.",
                StatusCodes.Status400BadRequest);
        }
        return files;
    }

    private static IReadOnlyList<string> ValidateFiles(IReadOnlyList<IFormFile> files, string label)
    {
        if (files.Count > RoleRequestFiles.MaxFilesPerField)
        {
            throw new RoleRequestException(
                "TOO_MANY_FILES",
                $"Upload at most {RoleRequestFiles.MaxFilesPerField} files for the {label}.",
                StatusCodes.Status400BadRequest);
        }

        var extensions = new List<string>(files.Count);
        foreach (var file in files)
        {
            if (file.Length > RoleRequestFiles.MaxBytes)
            {
                throw new RoleRequestException(
                    "FILE_TOO_LARGE",
                    $"Each {label} file must be 10 MB or smaller.",
                    StatusCodes.Status400BadRequest);
            }

            try
            {
                extensions.Add(RoleRequestFiles.RequireSafeExtension(file.FileName));
            }
            catch (InvalidOperationException ex)
            {
                throw new RoleRequestException("INVALID_FILE_TYPE", ex.Message, StatusCodes.Status400BadRequest);
            }
        }
        return extensions;
    }

    private async Task<string> SaveFilesAsync(
        int userId,
        IReadOnlyList<IFormFile> uploads,
        IReadOnlyList<string> extensions,
        CancellationToken ct)
    {
        var keys = new List<string>(uploads.Count);
        for (var i = 0; i < uploads.Count; i++)
        {
            var key = $"{userId}/{Guid.NewGuid():N}{extensions[i]}";
            await using var stream = uploads[i].OpenReadStream();
            await files.SaveAsync(key, stream, RoleRequestFiles.ContentType(extensions[i]), ct);
            keys.Add(key);
        }
        return RoleRequestFiles.JoinKeys(keys);
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
