using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ICertificateService
{
    Task<IReadOnlyList<CertificateDto>> GetMyAsync(int userId, CancellationToken ct = default);
    Task<CertificateFile?> OpenForMemberAsync(int userId, int certificateId, CancellationToken ct = default);
}

public sealed record CertificateFile(byte[] Bytes, string FileName);

public class CertificateService(CoLearnXDbContext db) : ICertificateService
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
                c.CertificateTemplate.Title,
                c.AwardedAt,
                c.VerificationCode,
                c.CourseId,
                db.Courses.Where(course => course.Id == c.CourseId).Select(course => course.Code).FirstOrDefault(),
                db.Courses.Where(course => course.Id == c.CourseId).Select(course => course.Title).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<CertificateFile?> OpenForMemberAsync(int userId, int certificateId, CancellationToken ct = default)
    {
        var certificate = await db.UserCertificates.AsNoTracking()
            .Include(item => item.CertificateTemplate)
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == certificateId && item.UserId == userId && item.AdminApproved, ct);
        return certificate is null ? null : await RenderAsync(certificate, ct);
    }

    private async Task<CertificateFile> RenderAsync(UserCertificate certificate, CancellationToken ct)
    {
        var course = await CourseAsync(certificate.CourseId, ct);
        var bytes = CertificatePdf.Create(certificate.User.FullName, course.Code, course.Title,
            certificate.CertificateTemplate.StageName, certificate.AwardedAt, certificate.VerificationCode);
        var stem = string.IsNullOrWhiteSpace(course.Code) ? "certificate" : course.Code;
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
