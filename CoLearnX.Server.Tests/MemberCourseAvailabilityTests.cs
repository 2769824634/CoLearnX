using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoLearnX.Server.Tests;

public class MemberCourseAvailabilityTests
{
    [Fact]
    public async Task DetailPreservesCancelledClassesHidesDraftsAndCountsReservationsAcrossSessions()
    {
        using var factory = new CoLearnXApiFactory();
        using var client = ApiClient.Anonymous(factory);
        int courseId, intakeId, cancelledId;
        var hiddenIds = new List<int>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoLearnXDbContext>();
            var intake = await db.CourseIntakes.Include(i => i.Sessions)
                .SingleAsync(i => i.Course.Code == "INFT 4010");
            courseId = intake.CourseId;
            intakeId = intake.Id;
            intake.Status = CourseIntakeStatus.Published;
            intake.MinEnrollment = 12;
            intake.ConfirmedToRunAt = DateTime.UtcNow;
            var first = intake.Sessions.Single();
            var second = new CourseSession { Label = "Second public session", CourseIntakeId = intake.Id,
                StartsAt = first.StartsAt, EndsAt = first.EndsAt };
            db.CourseSessions.Add(second);
            await db.SaveChangesAsync();
            var userId = await db.Users.Where(u => u.Email == SeedData.MemberEmail).Select(u => u.Id).SingleAsync();
            foreach (var (status, sessionId) in new[] { (EnrollmentStatus.Reserved, first.Id),
                (EnrollmentStatus.Active, second.Id), (EnrollmentStatus.Cancelled, first.Id),
                (EnrollmentStatus.Refunded, second.Id), (EnrollmentStatus.Completed, second.Id) })
                db.Enrollments.Add(new Enrollment { CourseId = courseId, CourseSessionId = sessionId,
                    UserId = userId, Status = status, CreditsSpent = 20 });
            var cancelled = new CourseIntake { CourseId = courseId, TrainerId = intake.TrainerId,
                Status = CourseIntakeStatus.Cancelled, CancelledAt = DateTime.UtcNow,
                RegistrationOpensAt = intake.RegistrationOpensAt, RegistrationClosesAt = intake.RegistrationClosesAt,
                StartsAt = intake.StartsAt, EndsAt = intake.EndsAt };
            cancelled.Sessions.Add(new CourseSession { Label = "Cancelled public session",
                StartsAt = first.StartsAt, EndsAt = first.EndsAt });
            db.CourseIntakes.Add(cancelled);
            foreach (var status in new[] { CourseIntakeStatus.Draft, CourseIntakeStatus.PendingApproval, CourseIntakeStatus.Rejected })
            {
                var hidden = new CourseIntake { CourseId = courseId, TrainerId = intake.TrainerId, Status = status,
                    RegistrationOpensAt = intake.RegistrationOpensAt, RegistrationClosesAt = intake.RegistrationClosesAt,
                    StartsAt = intake.StartsAt, EndsAt = intake.EndsAt };
                hidden.Sessions.Add(new CourseSession { Label = "Private schedule", StartsAt = first.StartsAt, EndsAt = first.EndsAt });
                db.CourseIntakes.Add(hidden);
                await db.SaveChangesAsync();
                hiddenIds.Add(hidden.Id);
            }
            await db.SaveChangesAsync();
            cancelledId = cancelled.Id;
        }

        var response = await client.GetFromJsonAsync<JsonObject>($"/api/courses/{courseId}");
        var sessions = response!["sessions"]!.AsArray();
        Assert.DoesNotContain(sessions, s => hiddenIds.Contains(s!["courseIntakeId"]!.GetValue<int>()));
        var publicSessions = sessions.Where(s => s!["courseIntakeId"]!.GetValue<int>() == intakeId).ToList();
        Assert.Equal(2, publicSessions.Count);
        Assert.All(publicSessions, s => {
            Assert.Equal(2, s!["intakeEnrollmentCount"]?.GetValue<int>());
            Assert.Equal(12, s["minEnrollment"]!.GetValue<int>());
            Assert.Equal("Published", s["intakeStatus"]?.GetValue<string>());
            Assert.NotNull(s["confirmedToRunAt"]);
        });
        var cancelledSession = Assert.Single(sessions, s => s!["courseIntakeId"]!.GetValue<int>() == cancelledId);
        Assert.Equal("Cancelled", cancelledSession!["intakeStatus"]?.GetValue<string>());
        Assert.NotNull(cancelledSession["cancelledAt"]);
    }
}
