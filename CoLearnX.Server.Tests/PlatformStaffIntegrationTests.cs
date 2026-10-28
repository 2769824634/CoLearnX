using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class PlatformStaffIntegrationTests(CoLearnXApiFactory factory)
    : IClassFixture<CoLearnXApiFactory>
{
    [Fact]
    public async Task Admin_provisions_staff_with_hidden_trainer_identity_in_one_request()
    {
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        using var form = new MultipartFormDataContent
        {
            { new StringContent("trainer.one@example.com"), "email" },
            { new StringContent("Trainer One"), "fullName" },
            { new StringContent("0400000001"), "phone" },
        };
        var document = new ByteArrayContent("staff-document"u8.ToArray());
        document.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(document, "document", "trainer-one.pdf");

        var response = await admin.PostAsync("/api/admin/staff", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("initialPassword").GetString()));
        var staff = body.RootElement.GetProperty("staff");
        var staffNumber = staff.GetProperty("staffNumber").GetInt32();
        Assert.True(staffNumber > 0);
        Assert.Equal($"CoLearnX 讲师 {staffNumber:000}", staff.GetProperty("displayName").GetString());
        Assert.Equal("trainer.one@example.com", staff.GetProperty("email").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var hiddenUser = await db.Users
            .Include(user => user.Roles)
            .Include(user => user.TrainerProfile)
            .SingleAsync(user => user.Email == $"staff-{staffNumber}@staff.colearnx");
        Assert.Equal($"CoLearnX 讲师 {staffNumber:000}", hiddenUser.FullName);
        Assert.Equal(new[] { AppRole.Trainer }, hiddenUser.Roles.Select(role => role.Role));
        Assert.NotNull(hiddenUser.TrainerProfile);
        Assert.Equal(0, hiddenUser.CreditBalance);
        Assert.Equal(0, hiddenUser.HeldCredits);

        var audit = await db.AuditLogs.OrderByDescending(log => log.Id)
            .FirstAsync(log => log.Action == "StaffAccountProvisioned");
        Assert.NotNull(audit.AdminAccountId);
        Assert.Null(audit.UserId);
    }

    [Fact]
    public async Task Staff_uses_a_separate_login_and_inactive_accounts_lose_existing_sessions()
    {
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        var provisioned = await ProvisionAsync(admin, "trainer.two@example.com", "Trainer Two");
        using var staffClient = factory.CreateClient();

        var login = await staffClient.PostAsJsonAsync("/api/staff/auth/login", new
        {
            email = "trainer.two@example.com",
            password = provisioned.InitialPassword,
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = loginBody.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        staffClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await staffClient.GetAsync("/api/staff/auth/me")).StatusCode);

        var disabled = await admin.PatchAsJsonAsync(
            $"/api/admin/staff/{provisioned.StaffId}/active",
            new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staffClient.GetAsync("/api/staff/auth/me")).StatusCode);

        using var freshClient = factory.CreateClient();
        var relogin = await freshClient.PostAsJsonAsync("/api/staff/auth/login", new
        {
            email = "trainer.two@example.com",
            password = provisioned.InitialPassword,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, relogin.StatusCode);
    }

    [Fact]
    public async Task Staff_creates_a_platform_owned_course_under_the_CoLearnX_system_creator()
    {
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        var provisioned = await ProvisionAsync(admin, "trainer.three@example.com", "Trainer Three");
        using var staffClient = await LoginAsStaffAsync("trainer.three@example.com", provisioned.InitialPassword);
        int levelId;
        int pathId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            levelId = await db.CourseLevels.OrderBy(level => level.SortOrder).Select(level => level.Id).FirstAsync();
            pathId = await db.LearningPaths.OrderBy(path => path.Id).Select(path => path.Id).FirstAsync();
        }

        var response = await staffClient.PostAsJsonAsync("/api/staff/courses", new
        {
            code = $"PLT-{Guid.NewGuid():N}"[..16],
            title = "Platform Skills Course",
            description = "Created by platform staff.",
            courseLevelId = levelId,
            learningPathId = pathId,
            category = "IT & Data",
            creditCost = 30,
            learningOutcomes = new[] { "Use the platform workflow" },
            interestIds = Array.Empty<int>(),
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("isPlatformOwned").GetBoolean());
        Assert.Equal("CoLearnX", body.RootElement.GetProperty("creatorName").GetString());

        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var courseId = body.RootElement.GetProperty("id").GetInt32();
        var course = await verificationDb.Courses.Include(item => item.Creator).SingleAsync(item => item.Id == courseId);
        var account = await verificationDb.StaffAccounts.SingleAsync(item => item.Id == provisioned.StaffId);
        Assert.Equal(account.UserId, course.TrainerId);
        Assert.Equal("CoLearnX", course.Creator.FullName);
        Assert.True(course.IsPlatformOwned);
        var audit = await verificationDb.AuditLogs.SingleAsync(log =>
            log.Action == "PlatformCourseCreated" && log.EntityId == courseId.ToString());
        Assert.Equal(provisioned.StaffId, audit.StaffAccountId);
        Assert.Null(audit.AdminAccountId);
        Assert.Null(audit.UserId);
    }

    [Fact]
    public async Task Public_platform_catalog_returns_only_published_platform_courses()
    {
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        var provisioned = await ProvisionAsync(admin, "trainer.four@example.com", "Trainer Four");
        using var staffClient = await LoginAsStaffAsync("trainer.four@example.com", provisioned.InitialPassword);
        int levelId;
        int pathId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            levelId = await db.CourseLevels.Select(item => item.Id).FirstAsync();
            pathId = await db.LearningPaths.Select(item => item.Id).FirstAsync();
        }
        var code = $"PUB-{Guid.NewGuid():N}"[..16];
        var create = await staffClient.PostAsJsonAsync("/api/staff/courses", new
        {
            code,
            title = "Published Platform Course",
            courseLevelId = levelId,
            learningPathId = pathId,
            category = "IT & Data",
            creditCost = 20,
            learningOutcomes = Array.Empty<string>(),
            interestIds = Array.Empty<int>(),
        });
        create.EnsureSuccessStatusCode();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            await db.Courses.Where(item => item.Code == code)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, CourseStatus.Published));
        }

        using var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync("/api/courses/platform?category=IT%20%26%20Data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var course = Assert.Single(body.RootElement.EnumerateArray(), item =>
            item.GetProperty("code").GetString() == code);
        Assert.True(course.GetProperty("isPlatformOwned").GetBoolean());
        Assert.Equal("CoLearnX", course.GetProperty("creatorName").GetString());
    }

    [Fact]
    public async Task Internal_staff_and_system_users_cannot_use_member_login_even_with_a_known_hash()
    {
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        var provisioned = await ProvisionAsync(admin, "trainer.five@example.com", "Trainer Five");
        const string knownPassword = "KnownPassword123!";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var staff = await db.StaffAccounts.SingleAsync(item => item.Id == provisioned.StaffId);
            var internalUsers = await db.Users.Where(item => item.Id == staff.UserId
                    || item.Email == CoLearnX.Server.Domain.PlatformIdentity.Email)
                .ToListAsync();
            foreach (var user in internalUsers)
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(knownPassword);
            await db.SaveChangesAsync();
        }
        using var anonymous = factory.CreateClient();

        var hiddenStaffLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new
        {
            email = (await InternalStaffEmailAsync(provisioned.StaffId)),
            password = knownPassword,
            activeRole = "Trainer",
        });
        var systemCreatorLogin = await anonymous.PostAsJsonAsync("/api/auth/login", new
        {
            email = CoLearnX.Server.Domain.PlatformIdentity.Email,
            password = knownPassword,
            activeRole = "Creator",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, hiddenStaffLogin.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, systemCreatorLogin.StatusCode);
    }

    [Fact]
    public async Task Staff_opens_an_intake_for_an_owned_platform_course_with_staff_only_audit()
    {
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);
        var provisioned = await ProvisionAsync(admin, "trainer.six@example.com", "Trainer Six");
        using var staffClient = await LoginAsStaffAsync("trainer.six@example.com", provisioned.InitialPassword);
        int levelId;
        int pathId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            levelId = await db.CourseLevels.Select(item => item.Id).FirstAsync();
            pathId = await db.LearningPaths.Select(item => item.Id).FirstAsync();
        }
        var createCourse = await staffClient.PostAsJsonAsync("/api/staff/courses", new
        {
            code = $"INT-{Guid.NewGuid():N}"[..16],
            title = "Platform Intake Course",
            courseLevelId = levelId,
            learningPathId = pathId,
            category = "IT & Data",
            creditCost = 20,
            learningOutcomes = Array.Empty<string>(),
            interestIds = Array.Empty<int>(),
        });
        createCourse.EnsureSuccessStatusCode();
        using var courseBody = JsonDocument.Parse(await createCourse.Content.ReadAsStringAsync());
        var courseId = courseBody.RootElement.GetProperty("id").GetInt32();
        int staffUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            staffUserId = await db.StaffAccounts.Where(item => item.Id == provisioned.StaffId)
                .Select(item => item.UserId).SingleAsync();
            await db.Courses.Where(item => item.Id == courseId)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.Status, CourseStatus.Published));
        }
        var now = DateTime.UtcNow;

        var response = await staffClient.PostAsJsonAsync($"/api/staff/courses/{courseId}/intakes", new
        {
            registrationOpensAt = now.AddDays(1),
            registrationClosesAt = now.AddDays(3),
            startsAt = now.AddDays(5),
            endsAt = now.AddDays(6),
            trainerId = -999,
            minEnrollment = 2,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var intakeId = body.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(staffUserId, body.RootElement.GetProperty("trainerId").GetInt32());
        var version = body.RootElement.GetProperty("version").GetGuid();
        var addSession = await staffClient.PostAsJsonAsync($"/api/staff/intakes/{intakeId}/sessions", new
        {
            label = "Live workshop",
            startsAt = now.AddDays(5).AddHours(1),
            endsAt = now.AddDays(5).AddHours(2),
            meetingLink = "https://meet.example.test/platform",
            physicalAddress = (string?)null,
            physicalCapacity = 0,
            physicalBookingDeadline = (DateTime?)null,
            version,
        });
        Assert.Equal(HttpStatusCode.OK, addSession.StatusCode);
        using var sessionBody = JsonDocument.Parse(await addSession.Content.ReadAsStringAsync());
        var sessionId = sessionBody.RootElement.GetProperty("sessions")[0].GetProperty("id").GetInt32();
        version = sessionBody.RootElement.GetProperty("version").GetGuid();
        var publish = await staffClient.PostAsJsonAsync($"/api/staff/intakes/{intakeId}/publish", new { version });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        using var publishBody = JsonDocument.Parse(await publish.Content.ReadAsStringAsync());
        Assert.Equal("Published", publishBody.RootElement.GetProperty("status").GetString());
        version = publishBody.RootElement.GetProperty("version").GetGuid();
        var updateDelivery = await staffClient.PatchAsJsonAsync(
            $"/api/staff/intakes/{intakeId}/sessions/{sessionId}/delivery",
            new { meetingLink = "https://meet.example.test/updated", version });
        Assert.True(updateDelivery.StatusCode == HttpStatusCode.OK,
            $"Expected OK, got {updateDelivery.StatusCode}: {await updateDelivery.Content.ReadAsStringAsync()}");
        using var verificationScope = factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var audit = await verificationDb.AuditLogs.SingleAsync(log =>
            log.Action == "CourseIntakeCreated" && log.EntityId == intakeId.ToString());
        Assert.Equal(provisioned.StaffId, audit.StaffAccountId);
        Assert.Null(audit.AdminAccountId);
        Assert.Null(audit.UserId);
        var staffAuditCount = await verificationDb.AuditLogs.CountAsync(log =>
            log.StaffAccountId == provisioned.StaffId && log.EntityId == intakeId.ToString());
        Assert.Equal(4, staffAuditCount);
        var deliveryAudit = await verificationDb.AuditLogs.SingleAsync(log =>
            log.Action == "CourseSessionDeliveryUpdated" && log.EntityId == intakeId.ToString());
        Assert.Equal(provisioned.StaffId, deliveryAudit.StaffAccountId);
        Assert.Null(deliveryAudit.UserId);
    }

    private async Task<(int StaffId, string InitialPassword)> ProvisionAsync(
        HttpClient admin,
        string email,
        string fullName)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(email), "email" },
            { new StringContent(fullName), "fullName" },
        };
        var document = new ByteArrayContent("staff-document"u8.ToArray());
        document.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(document, "document", "staff.pdf");
        var response = await admin.PostAsync("/api/admin/staff", form);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            body.RootElement.GetProperty("staff").GetProperty("id").GetInt32(),
            body.RootElement.GetProperty("initialPassword").GetString()!);
    }

    private async Task<HttpClient> LoginAsStaffAsync(string email, string password)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/staff/auth/login", new { email, password });
        login.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            body.RootElement.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<string> InternalStaffEmailAsync(int staffId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        return await db.StaffAccounts.Where(item => item.Id == staffId).Select(item => item.User.Email).SingleAsync();
    }
}
