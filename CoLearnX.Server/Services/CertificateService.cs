using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface ICertificateService
{
    Task<IReadOnlyList<CertificateDto>> GetMyAsync(int userId, CancellationToken ct = default);
}

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
}
