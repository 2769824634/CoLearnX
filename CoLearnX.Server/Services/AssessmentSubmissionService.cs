using System.Data;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Services;

public interface IAssessmentSubmissionService
{
    Task<MemberAssessmentDto> SubmitAsync(int userId, int enrollmentId, int assessmentId, IReadOnlyList<IFormFile> files,
        CancellationToken ct = default);
    Task<MaterialFileResult> OpenOwnFileAsync(int userId, int enrollmentId, int assessmentId, int fileId,
        CancellationToken ct = default);
    Task<MaterialFileResult> OpenForTrainerAsync(int trainerUserId, int assessmentId, int fileId, CancellationToken ct = default);
}

// Members hand in one or more documents for an Assessment before its due time; the Intake's Trainer downloads them.
public sealed class AssessmentSubmissionService(CoLearnXDbContext db, IFileStorage files) : IAssessmentSubmissionService
{
    public async Task<MemberAssessmentDto> SubmitAsync(int userId, int enrollmentId, int assessmentId,
        IReadOnlyList<IFormFile> uploads, CancellationToken ct = default)
    {
        var enrollment = await RequireMemberEnrollmentAsync(userId, enrollmentId, ct);
        var assessment = await RequireAssessmentAsync(enrollment, assessmentId, ct);
        await RequireOpenSubmissionAsync(enrollment, assessment, ct);
        var checkedFiles = ValidateUploads(uploads);

        var savedKeys = new List<string>();
        try
        {
            var submittedAt = DateTime.UtcNow;
            var saved = new List<(string Key, string Name, string Format, long Size)>();
            foreach (var (upload, extension) in checkedFiles)
            {
                var key = $"assessment-submissions/{assessmentId}/{enrollmentId}/{Guid.NewGuid():N}{extension}";
                savedKeys.Add(key);
                await using (var stream = upload.OpenReadStream())
                    await files.SaveAsync(key, stream, MaterialFiles.ContentType(extension), ct);
                saved.Add((key, AssignmentFiles.CleanFileName(upload.FileName), MaterialFiles.FormatLabel(extension), upload.Length));
            }

            // Serialize the read-and-replace operation across app instances. The execution strategy
            // lets SQL Server retry a deadlock or transient error without re-uploading the files.
            var committed = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var currentEnrollment = await RequireMemberEnrollmentAsync(userId, enrollmentId, ct);
                var currentAssessment = await RequireAssessmentAsync(currentEnrollment, assessmentId, ct);
                await RequireOpenSubmissionAsync(currentEnrollment, currentAssessment, ct);
                var previous = await db.AssessmentSubmissionFiles
                    .Where(item => item.AssessmentId == assessmentId && item.EnrollmentId == enrollmentId)
                    .ToListAsync(ct);
                var rows = saved.Select(file => new AssessmentSubmissionFile
                {
                    AssessmentId = assessmentId,
                    EnrollmentId = enrollmentId,
                    OriginalFileName = file.Name,
                    FilePath = file.Key,
                    Format = file.Format,
                    SizeBytes = file.Size,
                    SubmittedAt = submittedAt,
                }).ToList();
                db.AssessmentSubmissionFiles.RemoveRange(previous);
                db.AssessmentSubmissionFiles.AddRange(rows);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return (Assessment: currentAssessment, Rows: rows, Previous: previous.Select(item => item.FilePath).ToList());
            });

            foreach (var oldKey in committed.Previous.Where(key => !savedKeys.Contains(key)))
            {
                try { await files.DeleteAsync(oldKey, ct); }
                catch { /* The new submission is committed; stale-file cleanup is best effort. */ }
            }
            return ToMemberDto(committed.Assessment, null, committed.Rows, canSubmit: true);
        }
        catch
        {
            foreach (var key in savedKeys)
            {
                try { await files.DeleteAsync(key, CancellationToken.None); }
                catch { /* Preserve the original upload or database error. */ }
            }
            throw;
        }
    }

    private async Task RequireOpenSubmissionAsync(Enrollment enrollment, Assessment assessment, CancellationToken ct)
    {
        if (enrollment.Status != EnrollmentStatus.Active)
            throw new LaterPhaseException("SUBMISSION_CLOSED", "Assignments can be submitted only while the class is active.", 409);
        if (IsPastDue(assessment.DueAt, DateTime.UtcNow))
            throw new LaterPhaseException("SUBMISSION_CLOSED", "The submission deadline has passed.", 409);
        if (await db.AssessmentResults.AnyAsync(item => item.AssessmentId == assessment.Id && item.EnrollmentId == enrollment.Id, ct))
            throw new LaterPhaseException("SUBMISSION_CLOSED", "This assessment has already been graded.", 409);
    }

    public async Task<MaterialFileResult> OpenOwnFileAsync(int userId, int enrollmentId, int assessmentId, int fileId,
        CancellationToken ct = default)
    {
        await RequireMemberEnrollmentAsync(userId, enrollmentId, ct);
        var file = await db.AssessmentSubmissionFiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == fileId && item.AssessmentId == assessmentId && item.EnrollmentId == enrollmentId, ct);
        return await OpenAsync(file, ct);
    }

    public async Task<MaterialFileResult> OpenForTrainerAsync(int trainerUserId, int assessmentId, int fileId,
        CancellationToken ct = default)
    {
        var file = await db.AssessmentSubmissionFiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == fileId && item.AssessmentId == assessmentId
                && item.Assessment.CourseIntake.TrainerId == trainerUserId, ct);
        return await OpenAsync(file, ct);
    }

    // Shared with the Member Learning Hub list so both use the same rule and clock.
    internal static bool IsPastDue(DateTime? dueAt, DateTime utcNow)
        => dueAt.HasValue && DateTime.SpecifyKind(dueAt.Value, DateTimeKind.Utc) < utcNow;

    internal static MemberAssessmentDto ToMemberDto(Assessment assessment, AssessmentResult? result,
        IReadOnlyCollection<AssessmentSubmissionFile> submitted, bool canSubmit)
        => new(assessment.Id, assessment.Title, assessment.MaxScore, assessment.PassScore, assessment.DueAt,
            result == null ? "Not graded" : result.Score >= assessment.PassScore ? "Passed" : "Not passed",
            result?.Score, result?.Feedback, result?.GradedAt,
            submitted.Count == 0 ? null : submitted.Max(item => item.SubmittedAt),
            submitted.OrderBy(item => item.Id).Select(ToFileDto).ToList(),
            canSubmit);

    internal static AssessmentSubmissionFileDto ToFileDto(AssessmentSubmissionFile item)
        => new(item.Id, item.OriginalFileName, item.Format, item.SizeBytes, item.SubmittedAt);

    private static List<(IFormFile File, string Extension)> ValidateUploads(IReadOnlyList<IFormFile> uploads)
    {
        if (uploads.Count == 0)
            throw new LaterPhaseException("INVALID_SUBMISSION", "Choose at least one document to submit.", field: "files");
        if (uploads.Count > AssignmentFiles.MaxFiles)
            throw new LaterPhaseException("INVALID_SUBMISSION", $"You can submit at most {AssignmentFiles.MaxFiles} documents.", field: "files");
        if (uploads.Sum(item => item.Length) > AssignmentFiles.MaxTotalBytes)
            throw new LaterPhaseException("INVALID_SUBMISSION", "All documents together must be 25 MB or smaller.", field: "files");
        var result = new List<(IFormFile, string)>();
        foreach (var upload in uploads)
        {
            if (upload.Length <= 0)
                throw new LaterPhaseException("INVALID_SUBMISSION", $"{AssignmentFiles.CleanFileName(upload.FileName)} is empty.", field: "files");
            if (upload.Length > AssignmentFiles.MaxBytes)
                throw new LaterPhaseException("INVALID_SUBMISSION", "Each document must be 10 MB or smaller.", field: "files");
            try { result.Add((upload, MaterialFiles.RequireSafeExtension(upload.FileName))); }
            catch (InvalidOperationException ex) { throw new LaterPhaseException("INVALID_SUBMISSION", ex.Message, field: "files"); }
        }
        return result;
    }

    private async Task<MaterialFileResult> OpenAsync(AssessmentSubmissionFile? file, CancellationToken ct)
    {
        if (file is null)
            throw new LaterPhaseException("SUBMISSION_FILE_NOT_FOUND", "The submitted document was not found.", 404);
        var stream = await files.OpenAsync(file.FilePath, ct)
            ?? throw new LaterPhaseException("SUBMISSION_FILE_NOT_FOUND", "The submitted document was not found.", 404);
        return new MaterialFileResult(stream, MaterialFiles.ContentType(Path.GetExtension(file.FilePath)), file.OriginalFileName);
    }

    private async Task<Enrollment> RequireMemberEnrollmentAsync(int userId, int enrollmentId, CancellationToken ct)
        => await db.Enrollments.AsNoTracking().Include(item => item.CourseSession)
            .FirstOrDefaultAsync(item => item.Id == enrollmentId && item.UserId == userId
                && item.User.IsActive && item.User.Roles.Any(role => role.Role == AppRole.Member)
                && (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.Completed), ct)
            ?? throw new LaterPhaseException("ENROLLMENT_NOT_FOUND", "Enrollment not found.", 404);

    private async Task<Assessment> RequireAssessmentAsync(Enrollment enrollment, int assessmentId, CancellationToken ct)
        => await db.Assessments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == assessmentId && item.CourseIntakeId == enrollment.CourseSession.CourseIntakeId, ct)
            ?? throw new LaterPhaseException("ASSESSMENT_NOT_FOUND", "Assessment not found.", 404);
}
