using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CoLearnX.Server.Tests;

public sealed class LaterPhaseWorkflowIntegrationTests
{
    [Fact]
    public async Task RevokedTrainerToken_CannotReadLaterPhaseMaterialLibrary()
    {
        using var factory = new LaterPhaseApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(200, AppRole.Trainer);
        await factory.ReadAsync(async db =>
        {
            db.UserRoles.Remove(await db.UserRoles.SingleAsync(item => item.UserId == 200 && item.Role == AppRole.Trainer));
            return await db.SaveChangesAsync();
        });

        using var response = await trainer.GetAsync("/api/trainer/learning-materials");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RevokedMemberToken_CannotReadOwnDisputes()
    {
        using var factory = new LaterPhaseApiFactory();
        await factory.InitializeAsync();
        using var member = factory.UserClient(202, AppRole.Member);
        await factory.ReadAsync(async db =>
        {
            db.UserRoles.Remove(await db.UserRoles.SingleAsync(item => item.UserId == 202 && item.Role == AppRole.Member));
            return await db.SaveChangesAsync();
        });

        using var response = await member.GetAsync("/api/disputes/my");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MaterialVersion_RejectsUnsafePath_AndCannotReverseCompletedReview()
    {
        using var factory = new LaterPhaseApiFactory();
        await factory.InitializeAsync();
        using var creator = factory.UserClient(203, AppRole.Creator);
        using var admin = factory.AdminClient();

        using var unsafeUpload = await creator.PostAsJsonAsync("/api/materials", new
        {
            title = "Unsafe",
            filePath = "javascript:alert(1)",
            format = "PDF",
            category = "Design",
        });
        Assert.Equal(HttpStatusCode.BadRequest, unsafeUpload.StatusCode);

        using var upload = await creator.PostAsJsonAsync("/api/materials", new
        {
            title = "Safe",
            filePath = "materials/safe.pdf",
            format = "PDF",
            category = "Design",
        });
        var versionId = (await Body(upload)).GetProperty("versionId").GetInt32();
        using var approved = await admin.PostAsJsonAsync($"/api/admin/material-versions/{versionId}/review",
            new { decision = "Approve", reason = "Checked" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using var reverse = await admin.PostAsJsonAsync($"/api/admin/material-versions/{versionId}/review",
            new { decision = "Reject", reason = "Changed mind" });
        Assert.Equal(HttpStatusCode.Conflict, reverse.StatusCode);
    }

    [Fact]
    public async Task TrainerDelivery_AttendanceRosterRecordingAndApprovedMaterial_AreIntakeScoped()
    {
        using var factory = new LaterPhaseApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(200, AppRole.Trainer);
        using var otherTrainer = factory.UserClient(201, AppRole.Trainer);
        using var creator = factory.UserClient(203, AppRole.Creator);
        using var admin = factory.AdminClient();

        using var upload = await creator.PostAsJsonAsync("/api/materials", new
        {
            title = "Later Phase workbook",
            description = "Trainer delivery resource",
            filePath = "materials/later-phase-v1.pdf",
            format = "PDF",
            category = "Design",
        });
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var uploaded = await Body(upload);
        var versionId = uploaded.GetProperty("versionId").GetInt32();

        using var review = await admin.PostAsJsonAsync($"/api/admin/material-versions/{versionId}/review",
            new { decision = "Approve", reason = "Checked" });
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);

        using var attach = await trainer.PostAsJsonAsync("/api/trainer/intakes/200/learning-materials",
            new { materialVersionId = versionId });
        Assert.Equal(HttpStatusCode.OK, attach.StatusCode);

        using var recording = await trainer.PostAsJsonAsync("/api/trainer/intakes/200/sessions/200/recordings",
            new { title = "Session replay", recordingUrl = "https://video.example/replay/200" });
        Assert.Equal(HttpStatusCode.Created, recording.StatusCode);

        using var attendance = await trainer.PutAsJsonAsync("/api/trainer/intakes/200/sessions/200/attendance",
            new { records = new[] { new { enrollmentId = 200, status = "Present" }, new { enrollmentId = 201, status = "Late" } } });
        Assert.Equal(HttpStatusCode.OK, attendance.StatusCode);

        using var rosterResponse = await trainer.GetAsync("/api/trainer/intakes/200/learners");
        Assert.Equal(HttpStatusCode.OK, rosterResponse.StatusCode);
        var roster = await Body(rosterResponse);
        Assert.Equal(2, roster.GetArrayLength());
        Assert.Contains(roster.EnumerateArray(), item => item.GetProperty("attendanceRate").GetInt32() == 100);

        using var forbidden = await otherTrainer.GetAsync("/api/trainer/intakes/200/learners");
        Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    [Fact]
    public async Task AssessmentAndCertificate_RequireTrainerReviewBeforeAdminIssuance()
    {
        using var factory = new LaterPhaseApiFactory();
        await factory.InitializeAsync();
        using var trainer = factory.UserClient(200, AppRole.Trainer);
        using var member = factory.UserClient(202, AppRole.Member);
        using var admin = factory.AdminClient();

        using var attendance = await trainer.PutAsJsonAsync("/api/trainer/intakes/200/sessions/200/attendance",
            new { records = new[] { new { enrollmentId = 200, status = "Present" } } });
        Assert.Equal(HttpStatusCode.OK, attendance.StatusCode);

        using var createAssessment = await trainer.PostAsJsonAsync("/api/trainer/intakes/200/assessments",
            new { title = "Final assessment", maxScore = 100, passScore = 60, dueAt = "2026-10-01T00:00:00Z" });
        Assert.Equal(HttpStatusCode.Created, createAssessment.StatusCode);
        var assessmentId = (await Body(createAssessment)).GetProperty("id").GetInt32();

        using var grade = await trainer.PutAsJsonAsync($"/api/trainer/assessments/{assessmentId}/grades/200",
            new { score = 88, feedback = "Ready for certification" });
        Assert.Equal(HttpStatusCode.OK, grade.StatusCode);

        using var submit = await member.PostAsJsonAsync("/api/certificates/requests", new { enrollmentId = 200 });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var requestId = (await Body(submit)).GetProperty("id").GetInt32();

        using var premature = await admin.PostAsJsonAsync($"/api/admin/certificate-requests/{requestId}/review",
            new { decision = "Approve", reason = "Too early" });
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);

        using var trainerReview = await trainer.PostAsJsonAsync($"/api/trainer/certificate-requests/{requestId}/review",
            new { decision = "Approve", reason = "Attendance and assessment verified" });
        Assert.Equal(HttpStatusCode.OK, trainerReview.StatusCode);

        using var adminReview = await admin.PostAsJsonAsync($"/api/admin/certificate-requests/{requestId}/review",
            new { decision = "Approve", reason = "Final approval" });
        Assert.Equal(HttpStatusCode.OK, adminReview.StatusCode);
        var issued = await Body(adminReview);
        Assert.Equal("Issued", issued.GetProperty("status").GetString());
        Assert.True(issued.GetProperty("certificateId").GetInt32() > 0);

        using var certificates = await member.GetAsync("/api/certificates/my");
        Assert.Equal(HttpStatusCode.OK, certificates.StatusCode);
        Assert.Single((await Body(certificates)).EnumerateArray());
    }

    [Fact]
    public async Task AdminCreditAdjustment_IsIdempotentAndAudited()
    {
        using var factory = new LaterPhaseApiFactory();
        await factory.InitializeAsync();
        using var admin = factory.AdminClient();
        var key = Guid.NewGuid();
        var request = new { userId = 202, delta = 15, reason = "Support correction", idempotencyKey = key };

        using var first = await admin.PostAsJsonAsync("/api/admin/credits/adjustments", request);
        using var repeated = await admin.PostAsJsonAsync("/api/admin/credits/adjustments", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(115, (await Body(repeated)).GetProperty("balanceAfter").GetInt32());

        Assert.Equal(1, await factory.ReadAsync(db => db.CreditTransactions.CountAsync(t => t.IdempotencyKey == key)));
        Assert.Equal(1, await factory.ReadAsync(db => db.AuditLogs.CountAsync(l => l.Action == "CreditAdjusted" && l.AdminAccountId == 1)));

        using var conflict = await admin.PostAsJsonAsync("/api/admin/credits/adjustments",
            new { userId = 202, delta = 15, reason = "Different operation", idempotencyKey = key });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task DisputeRefund_ChangesBalanceOnceAndClosesCase()
    {
        using var factory = new LaterPhaseApiFactory();
        await factory.InitializeAsync();
        using var member = factory.UserClient(204, AppRole.Member);
        using var admin = factory.AdminClient();

        using var submitted = await member.PostAsJsonAsync("/api/disputes",
            new { enrollmentId = 201, reason = "Duplicate enrolment" });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var disputeId = (await Body(submitted)).GetProperty("id").GetInt32();
        var key = Guid.NewGuid();
        var reviewRequest = new { decision = "Refund", refundCredits = 25, reason = "Duplicate confirmed", idempotencyKey = key };

        using var refunded = await admin.PostAsJsonAsync($"/api/admin/disputes/{disputeId}/review", reviewRequest);
        using var repeated = await admin.PostAsJsonAsync($"/api/admin/disputes/{disputeId}/review", reviewRequest);
        Assert.Equal(HttpStatusCode.OK, refunded.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var body = await Body(repeated);
        Assert.Equal("ResolvedRefund", body.GetProperty("status").GetString());
        Assert.Equal(125, body.GetProperty("balanceAfter").GetInt32());

        Assert.Equal(1, await factory.ReadAsync(db => db.CreditTransactions.CountAsync(t => t.RelatedDisputeId == disputeId)));
        Assert.Equal(EnrollmentStatus.Refunded, await factory.ReadAsync(db => db.Enrollments.Where(e => e.Id == 201).Select(e => e.Status).SingleAsync()));

        using var reverse = await admin.PostAsJsonAsync($"/api/admin/disputes/{disputeId}/review",
            new { decision = "Reject", refundCredits = (int?)null, reason = "Different result", idempotencyKey = key });
        Assert.Equal(HttpStatusCode.Conflict, reverse.StatusCode);
    }

    private static async Task<JsonElement> Body(HttpResponseMessage response)
        => (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement.Clone();

    private sealed class LaterPhaseApiFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _connection.Open();
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<CoLearnXDbContext>>();
                services.RemoveAll<CoLearnXDbContext>();
                services.AddDbContext<CoLearnXDbContext>(options => options.UseSqlite(_connection));
            });
        }

        public HttpClient UserClient(int id, AppRole activeRole)
        {
            using var scope = Services.CreateScope();
            var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
                .CreateToken(new User { Id = id }, activeRole, [activeRole]).Token;
            return WithToken(token);
        }

        public HttpClient AdminClient()
        {
            using var scope = Services.CreateScope();
            var token = scope.ServiceProvider.GetRequiredService<IAdminTokenService>()
                .CreateToken(new AdminAccount { Id = 1, Email = SeedData.AdminEmail }).Token;
            return WithToken(token);
        }

        private HttpClient WithToken(string token)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public async Task InitializeAsync()
        {
            await ReadAsync(async db =>
            {
                db.Users.AddRange(
                    new User { Id = 200, Email = "later-trainer@example.com", FullName = "Later Trainer", IsActive = true, Roles = [new UserRole { Role = AppRole.Trainer }] },
                    new User { Id = 201, Email = "later-other@example.com", FullName = "Other Trainer", IsActive = true, Roles = [new UserRole { Role = AppRole.Trainer }] },
                    new User { Id = 202, Email = "later-member@example.com", FullName = "Later Member", CreditBalance = 100, IsActive = true, Roles = [new UserRole { Role = AppRole.Member }] },
                    new User { Id = 203, Email = "later-creator@example.com", FullName = "Later Creator", IsActive = true, Roles = [new UserRole { Role = AppRole.Creator }] },
                    new User { Id = 204, Email = "later-dispute@example.com", FullName = "Dispute Member", CreditBalance = 100, IsActive = true, Roles = [new UserRole { Role = AppRole.Member }] });
                db.Courses.Add(new Course
                {
                    Id = 200, Code = "LP-200", Title = "Later Phase", TrainerId = 200, CreatorId = 203,
                    CreditCost = 25, Status = CourseStatus.Published,
                });
                db.CourseIntakes.Add(new CourseIntake
                {
                    Id = 200, CourseId = 200, TrainerId = 200,
                    RegistrationOpensAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    RegistrationClosesAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                    StartsAt = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
                    EndsAt = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc),
                    Status = CourseIntakeStatus.InProgress,
                });
                db.CourseSessions.Add(new CourseSession
                {
                    Id = 200, CourseIntakeId = 200, Label = "Workshop",
                    StartsAt = new DateTime(2026, 9, 1, 1, 0, 0, DateTimeKind.Utc),
                    EndsAt = new DateTime(2026, 9, 1, 3, 0, 0, DateTimeKind.Utc),
                    MeetingLink = "https://meet.example/200",
                });
                db.Enrollments.AddRange(
                    new Enrollment { Id = 200, UserId = 202, CourseId = 200, CourseSessionId = 200, Status = EnrollmentStatus.Completed, ProgressPercent = 100, CreditsSpent = 25, CompletedAt = DateTime.UtcNow },
                    new Enrollment { Id = 201, UserId = 204, CourseId = 200, CourseSessionId = 200, Status = EnrollmentStatus.Active, ProgressPercent = 50, CreditsSpent = 25 });
                return await db.SaveChangesAsync();
            });
        }

        public async Task<T> ReadAsync<T>(Func<CoLearnXDbContext, Task<T>> action)
        {
            using var scope = Services.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>());
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) _connection.Dispose();
        }
    }
}
