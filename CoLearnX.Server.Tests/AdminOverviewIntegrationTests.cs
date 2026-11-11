using System.Net;
using System.Net.Http.Json;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public sealed class AdminOverviewIntegrationTests(CoLearnXApiFactory factory) : IClassFixture<CoLearnXApiFactory>
{
    [Fact]
    public async Task Class_overview_counts_learners_and_attendance_from_held_sessions_only()
    {
        var setup = await CreateClassAsync();
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);

        var rows = await admin.GetFromJsonAsync<List<AdminClassRowDto>>("/api/admin/classes");
        var row = Assert.Single(rows!, r => r.Id == setup.ClassId);
        Assert.Equal(2, row.Learners);
        Assert.Equal(2, row.SessionCount);
        Assert.Equal(1, row.SessionsHeld);
        // One of two learners attended the only held session.
        Assert.Equal(0.5, row.AttendanceRate);
        Assert.Equal("Overview Creator", row.CreatorName);
        Assert.Equal("Overview Trainer", row.TrainerName);
        Assert.Equal("InProgress", row.Status);

        var detail = await admin.GetFromJsonAsync<AdminClassDetailDto>($"/api/admin/classes/{setup.ClassId}");
        Assert.Equal(2, detail!.Sessions.Count);
        Assert.True(detail.Sessions[0].Held);
        Assert.Equal(1, detail.Sessions[0].Attended);
        Assert.False(detail.Sessions[1].Held);
        var present = Assert.Single(detail.Learners, l => l.UserId == setup.PresentLearnerId);
        Assert.Equal(1.0, present.AttendanceRate);
        Assert.Equal("Submitted", present.CertificateStatus);
        var absent = Assert.Single(detail.Learners, l => l.UserId == setup.AbsentLearnerId);
        Assert.Equal(0.0, absent.AttendanceRate);
        Assert.Null(absent.CertificateStatus);
        Assert.Contains(detail.Materials, m => m.Title == "Week 1 slides" && m.Scope == "Kick-off");

        var missing = await admin.GetAsync("/api/admin/classes/999999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task User_activity_shows_teaching_created_courses_and_learning()
    {
        var setup = await CreateClassAsync();
        using var admin = await ApiClient.AsOperationsAdminAsync(factory);

        var trainer = await admin.GetFromJsonAsync<AdminUserActivityDto>($"/api/admin/users/{setup.TrainerId}/activity");
        Assert.Equal(["Trainer"], trainer!.Roles);
        Assert.Single(trainer.TeachingClasses, c => c.Id == setup.ClassId);
        Assert.Empty(trainer.CreatedCourses);

        var creator = await admin.GetFromJsonAsync<AdminUserActivityDto>($"/api/admin/users/{setup.CreatorId}/activity");
        var course = Assert.Single(creator!.CreatedCourses);
        Assert.Equal(1, course.ClassCount);
        Assert.Equal(2, course.LearnerCount);
        Assert.Equal(25, creator.RoyaltyCredits);

        var member = await admin.GetFromJsonAsync<AdminUserActivityDto>($"/api/admin/users/{setup.PresentLearnerId}/activity");
        var learning = Assert.Single(member!.Learning);
        Assert.Equal(setup.ClassId, learning.ClassId);
        Assert.Equal(1, learning.Attended);
        Assert.Equal(1.0, learning.AttendanceRate);
        Assert.Equal(200, member.TotalTopUpCredits);
        Assert.Equal(40, member.TotalSpentCredits);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/admin/users/999999/activity")).StatusCode);
    }

    [Fact]
    public async Task Overview_endpoints_require_an_admin()
    {
        var setup = await CreateClassAsync();
        using var member = await ApiClient.AsMemberAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/admin/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync($"/api/admin/classes/{setup.ClassId}")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/admin/users/{setup.TrainerId}/activity")).StatusCode);
    }

    private async Task<(int ClassId, int TrainerId, int CreatorId, int PresentLearnerId, int AbsentLearnerId)> CreateClassAsync()
    {
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
        var tag = Guid.NewGuid().ToString("N")[..8];
        User NewUser(string name, AppRole role) => new()
        {
            Email = $"{name.Replace(' ', '.').ToLowerInvariant()}-{tag}@example.test", FullName = name, DisplayName = name,
            Roles = [new UserRole { Role = role }],
        };
        var trainer = NewUser("Overview Trainer", AppRole.Trainer);
        var creator = NewUser("Overview Creator", AppRole.Creator);
        var present = NewUser("Present Learner", AppRole.Member);
        var absent = NewUser("Absent Learner", AppRole.Member);
        db.Users.AddRange(trainer, creator, present, absent);
        await db.SaveChangesAsync();

        var start = DateTime.UtcNow.Date.AddDays(-3);
        var course = new Course { Code = $"OVR-{tag}", Title = "Overview workshop", TrainerId = trainer.Id,
            CreatorId = creator.Id, Status = CourseStatus.Published, CreditCost = 40 };
        var held = new CourseSession { Label = "Kick-off", StartsAt = start.AddHours(1), EndsAt = start.AddHours(3) };
        var future = new CourseSession { Label = "Wrap-up", StartsAt = start.AddDays(10), EndsAt = start.AddDays(10).AddHours(2) };
        var intake = new CourseIntake { Course = course, TrainerId = trainer.Id, StartsAt = start, EndsAt = start.AddDays(14),
            RegistrationOpensAt = start.AddDays(-20), RegistrationClosesAt = start.AddDays(-5),
            Status = CourseIntakeStatus.InProgress, MinEnrollment = 2, Sessions = [held, future] };
        db.CourseIntakes.Add(intake);
        await db.SaveChangesAsync();

        var presentEnrollment = new Enrollment { UserId = present.Id, CourseId = course.Id, CourseSessionId = held.Id,
            Status = EnrollmentStatus.Active, CreditsSpent = 40 };
        db.Enrollments.AddRange(presentEnrollment, new Enrollment { UserId = absent.Id, CourseId = course.Id,
            CourseSessionId = held.Id, Status = EnrollmentStatus.Active, CreditsSpent = 40 });
        db.AttendanceRecords.AddRange(
            new AttendanceRecord { CourseSessionId = held.Id, UserId = present.Id, Status = AttendanceStatus.Present, RecordedByTrainerId = trainer.Id },
            new AttendanceRecord { CourseSessionId = held.Id, UserId = absent.Id, Status = AttendanceStatus.Absent, RecordedByTrainerId = trainer.Id });
        db.SessionMaterials.Add(new SessionMaterial { CourseSessionId = held.Id, AddedByTrainerId = trainer.Id,
            Title = "Week 1 slides", FilePath = "materials/week1.pdf" });
        db.CreditTransactions.AddRange(
            new CreditTransaction { UserId = present.Id, Type = CreditTransactionType.TopUp, Delta = 200, BalanceAfter = 200, Description = "Top up" },
            new CreditTransaction { UserId = creator.Id, Type = CreditTransactionType.Royalty, Delta = 25, BalanceAfter = 25, Description = "Royalty" });
        await db.SaveChangesAsync();
        db.CertificateRequests.Add(new CertificateRequest { EnrollmentId = presentEnrollment.Id });
        await db.SaveChangesAsync();

        return (intake.Id, trainer.Id, creator.Id, present.Id, absent.Id);
    }
}
