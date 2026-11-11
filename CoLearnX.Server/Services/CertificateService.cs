using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ICertificateService
{
    Task<IReadOnlyList<CertificateDto>> GetMyAsync(int userId, CancellationToken ct = default);
    Task<CertificateFile?> OpenForMemberAsync(int userId, int certificateId, CancellationToken ct = default);
}

public sealed record CertificateFile(byte[] Bytes, string FileName);

public class CertificateService(CoLearnXDbContext db, IFileStorage files) : ICertificateService
{
    public async Task<IReadOnlyList<CertificateDto>> GetMyAsync(int userId, CancellationToken ct = default)
    {
        return await db.UserCertificates.AsNoTracking()
            .Include(c => c.CertificateTemplate)
            .Where(c => c.UserId == userId && c.AdminApproved)
            .OrderBy(c => c.CertificateTemplate.StageNumber)
            .Select(c => new CertificateDto(
                c.Id,
                c.CertificateTemplate.StageName,
                c.CertificateTemplate.StageNumber,
                c.CourseId == null ? c.CertificateTemplate.Title : "Course Certificate",
                c.AwardedAt,
                c.VerificationCode,
                c.CourseId,
                db.Courses.Where(course => course.Id == c.CourseId).Select(course => course.Code).FirstOrDefault(),
                db.Courses.Where(course => course.Id == c.CourseId).Select(course => course.Title).FirstOrDefault(),
                c.LearningPathId,
                db.LearningPaths.Where(path => path.Id == c.LearningPathId).Select(path => path.Name).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<CertificateFile?> OpenForMemberAsync(int userId, int certificateId, CancellationToken ct = default)
    {
        var certificate = await db.UserCertificates.AsNoTracking()
            .Include(item => item.CertificateTemplate)
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == certificateId && item.UserId == userId && item.AdminApproved, ct);
        if (certificate is null) return null;
        if (!string.IsNullOrWhiteSpace(certificate.FilePath))
        {
            await using var stored = await files.OpenAsync(certificate.FilePath, ct);
            if (stored is null) return null;
            using var buffer = new MemoryStream();
            await stored.CopyToAsync(buffer, ct);
            return new CertificateFile(buffer.ToArray(), CertificateFiles.DownloadName(certificate.OriginalFileName));
        }
        return await RenderAsync(certificate, ct);
    }

    private async Task<CertificateFile> RenderAsync(UserCertificate certificate, CancellationToken ct)
    {
        var course = certificate.LearningPathId is int pathId
            ? (Code: "", Title: await db.LearningPaths.Where(p => p.Id == pathId).Select(p => p.Name).SingleAsync(ct))
            : await CourseAsync(certificate.CourseId, ct);
        var bytes = CertificatePdf.Create(certificate.User.FullName, course.Code, course.Title,
            certificate.CertificateTemplate.StageName, certificate.AwardedAt, certificate.VerificationCode, certificate.LearningPathId != null);
        var stem = string.IsNullOrWhiteSpace(course.Code) ? course.Title : course.Code;
        var safe = new string(stem.Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');
        return new CertificateFile(bytes, $"{(safe.Length == 0 ? "certificate" : safe)}.pdf");
    }

    private async Task<(string Code, string Title)> CourseAsync(int? courseId, CancellationToken ct)
    {
        if (courseId is not int id) return ("", "Course");
        var course = await db.Courses.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new { item.Code, item.Title }).SingleOrDefaultAsync(ct);
        return course is null ? ("", "Course") : (course.Code, course.Title);
    }
}
