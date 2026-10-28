using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoLearnX.Server.Auth;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class AdminPendingAlertsTests
{
    [Fact]
    public async Task Pending_counts_include_only_items_waiting_for_an_admin()
    {
        await using var factory = new CoLearnXApiFactory();
        using var admin = AdminClient(factory);
        var before = await admin.GetFromJsonAsync<AdminPendingCountsDto>("/api/admin/pending-counts");

        await WriteAsync(factory, async db =>
        {
            var creator = new User { Email = "pending-creator@example.com", FullName = "Pending Creator" };
            var applicant = new User { Email = "pending-applicant@example.com", FullName = "Pending Applicant" };
            db.Users.AddRange(creator, applicant);
            await db.SaveChangesAsync();

            db.RoleRequests.AddRange(
                new RoleRequest { UserId = applicant.Id, RequestedRole = AppRole.Trainer, Status = RoleRequestStatus.Pending },
                new RoleRequest { UserId = applicant.Id, RequestedRole = AppRole.Creator, Status = RoleRequestStatus.Approved });
            db.Courses.AddRange(
                new Course { Code = "PEND-1", Title = "Waiting", CreatorId = creator.Id, TrainerId = creator.Id, Status = CourseStatus.PendingApproval },
                new Course { Code = "PEND-2", Title = "Draft", CreatorId = creator.Id, TrainerId = creator.Id, Status = CourseStatus.Draft });
            var material = new LearningMaterial { CreatorId = creator.Id, Title = "Pending guide" };
            db.LearningMaterials.Add(material);
            await db.SaveChangesAsync();

            db.CourseMaterialVersions.AddRange(
                new CourseMaterialVersion { LearningMaterialId = material.Id, VersionNumber = 1, Status = MaterialVersionStatus.PendingApproval },
                new CourseMaterialVersion { LearningMaterialId = material.Id, VersionNumber = 2, Status = MaterialVersionStatus.Approved });
            var enrollmentIds = await db.Enrollments
                .Where(enrollment => !db.CertificateRequests.Any(request => request.EnrollmentId == enrollment.Id))
                .OrderBy(enrollment => enrollment.Id).Select(enrollment => enrollment.Id).Take(2).ToListAsync();
            Assert.Equal(2, enrollmentIds.Count);
            db.CertificateRequests.AddRange(
                new CertificateRequest { EnrollmentId = enrollmentIds[0], Status = CertificateRequestStatus.TrainerApproved },
                new CertificateRequest { EnrollmentId = enrollmentIds[1], Status = CertificateRequestStatus.Submitted });
            db.Disputes.AddRange(
                new Dispute { RaisedByUserId = applicant.Id, Reason = "Open", Status = DisputeStatus.Open },
                new Dispute { RaisedByUserId = applicant.Id, Reason = "Closed", Status = DisputeStatus.Rejected });
            await db.SaveChangesAsync();
        });

        var after = await admin.GetFromJsonAsync<AdminPendingCountsDto>("/api/admin/pending-counts");

        Assert.Equal(before! with
        {
            RoleRequests = before.RoleRequests + 1,
            Courses = before.Courses + 1,
            MaterialVersions = before.MaterialVersions + 1,
            Certificates = before.Certificates + 1,
            Disputes = before.Disputes + 1,
        }, after);
    }

    [Fact]
    public async Task Audit_filter_options_list_each_recorded_resource_and_result_once_in_order()
    {
        await using var factory = new CoLearnXApiFactory();
        using var admin = AdminClient(factory);
        await WriteAsync(factory, async db =>
        {
            var userId = await db.Users.Select(user => user.Id).FirstAsync();
            db.AuditLogs.AddRange(
                new AuditLog { UserId = userId, Action = "Test", EntityType = "Zebra", Result = "Zapped" },
                new AuditLog { UserId = userId, Action = "Test", EntityType = "Zebra", Result = "Zapped" },
                new AuditLog { UserId = userId, Action = "Test", EntityType = "Aardvark", Result = "Absorbed" });
            await db.SaveChangesAsync();
        });

        var options = await admin.GetFromJsonAsync<AuditLogFilterOptionsDto>("/api/admin/audit-logs/filters");

        Assert.Contains("Zebra", options!.EntityTypes);
        Assert.Contains("Aardvark", options.EntityTypes);
        Assert.Contains("Zapped", options.Results);
        Assert.Contains("Absorbed", options.Results);
        Assert.Equal(options.EntityTypes.Distinct().Order(StringComparer.Ordinal), options.EntityTypes);
        Assert.Equal(options.Results.Distinct().Order(StringComparer.Ordinal), options.Results);
    }

    [Theory]
    [InlineData("/api/admin/pending-counts")]
    [InlineData("/api/admin/audit-logs/filters")]
    public async Task Only_admins_can_read_pending_counts_and_audit_filters(string path)
    {
        await using var factory = new CoLearnXApiFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);

        using var member = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var user = await db.Users.FirstAsync();
            var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
                .CreateToken(user, AppRole.Member, [AppRole.Member]).Token;
            member.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        Assert.Contains((await member.GetAsync(path)).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    private static HttpClient AdminClient(CoLearnXApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var account = db.AdminAccounts.AsNoTracking().Single(item => item.Email == SeedData.AdminEmail);
        var token = scope.ServiceProvider.GetRequiredService<IAdminTokenService>().CreateToken(account).Token;
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task WriteAsync(CoLearnXApiFactory factory, Func<CoLearnXDbContext, Task> write)
    {
        using var scope = factory.Services.CreateScope();
        await write(scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>());
    }
}
