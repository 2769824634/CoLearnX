using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CoLearnX.Server.Data;

namespace CoLearnX.Server.Tests;

// The Creator fixes the whole schedule and sends it to the Trainer; the Intake is published when the Trainer accepts.
public class IntakeTrainerInvitationTests
{
    private const int Creator = 1;
    private const int Trainer = 2;
    private const int OtherTrainer = 5;
    private static DateTime FirstSession => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(10).AddHours(8), DateTimeKind.Utc);
    private static OpenClassRequest Weekly(int trainerId = Trainer) => new(TrainerId: trainerId,
        SessionStarts: [FirstSession, FirstSession.AddDays(7), FirstSession.AddDays(14)],
        MeetingLink: "https://example.com/meeting");

    [Fact]
    public async Task Opening_an_intake_sends_the_full_schedule_to_the_trainer()
    {
        await using var f = await Fixture.CreateAsync();

        var intake = await f.Intakes.OpenClassAsync(Creator, 1, Weekly() with { SendToTrainer = true });

        Assert.Equal("Draft", intake.Status);
        Assert.Equal("Invited", intake.TrainerInvitationStatus);
        Assert.NotNull(intake.TrainerInvitedAt);
        var notice = await f.Db.Notifications.SingleAsync(n => n.UserId == Trainer);
        Assert.Equal("N-intake-invitation", notice.Code);
        Assert.Equal(intake.Id, notice.IntakeId);
        Assert.True(notice.EmailPending);
        Assert.Contains("3 sessions", notice.Body);
        Assert.Equal("/trainer/invitations", NotificationTargetPath.For(notice.Code, notice.IntakeId));
    }

    [Fact]
    public async Task Opening_without_sending_keeps_a_draft_that_no_trainer_has_seen()
    {
        await using var f = await Fixture.CreateAsync();

        var intake = await f.Intakes.OpenClassAsync(Creator, 1, Weekly());

        Assert.Equal("Draft", intake.Status);
        Assert.Equal("NotSent", intake.TrainerInvitationStatus);
        Assert.False(await f.Db.Notifications.AnyAsync());
    }

    [Fact]
    public async Task An_intake_cannot_be_published_before_the_trainer_accepts()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Intakes.OpenClassAsync(Creator, 1, Weekly());

        var unsent = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Intakes.PublishAsync(Creator, draft.Id, draft.Version));
        Assert.Equal("TRAINER_NOT_CONFIRMED", unsent.Code);

        var invited = await f.Intakes.SendToTrainerAsync(Creator, draft.Id, draft.Version);
        var waiting = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Intakes.PublishAsync(Creator, invited.Id, invited.Version));
        Assert.Equal("TRAINER_NOT_CONFIRMED", waiting.Code);

        var reply = await f.Invitations.RespondAsync(Trainer, invited.Id, accept: true, reason: null);
        Assert.Equal("Accepted", reply.Status);
        Assert.Equal("Published", reply.IntakeStatus);
        var accepted = await f.Intakes.GetForCreatorAsync(Creator, invited.Id);
        Assert.Equal("Published", accepted.Status);
        Assert.Equal("Accepted", accepted.TrainerInvitationStatus);

        // Publishing again by hand changes nothing.
        var published = await f.Intakes.PublishAsync(Creator, accepted.Id, accepted.Version);
        Assert.Equal("Published", published.Status);
        Assert.Equal(accepted.Version, published.Version);
        Assert.Equal(1, await f.Db.AuditLogs.CountAsync(a => a.Action == "CourseIntakePublished" && a.EntityId == invited.Id.ToString()));
    }

    [Fact]
    public async Task Accepting_publishes_the_intake_exactly_as_the_creator_would()
    {
        await using var f = await Fixture.CreateAsync();
        var invited = await f.Intakes.OpenClassAsync(Creator, 1, Weekly() with { SendToTrainer = true });

        await f.Invitations.RespondAsync(Trainer, invited.Id, accept: true, reason: null);

        var intake = await f.Intakes.GetForCreatorAsync(Creator, invited.Id);
        Assert.Equal("Published", intake.Status);
        Assert.Equal(Creator, intake.ConfirmedByCreatorId);
        Assert.NotNull(intake.ConfirmedAt);
        Assert.NotNull(intake.SubmittedAt);
        var toCreator = await f.Db.Notifications.SingleAsync(n => n.UserId == Creator);
        Assert.Equal("N-intake-invitation-accepted", toCreator.Code);
        Assert.Contains("published automatically", toCreator.Body);
        Assert.DoesNotContain("You can publish it now", toCreator.Body);
        var toTrainer = await f.Db.Notifications.SingleAsync(n => n.UserId == Trainer && n.Code == "N-intake-published");
        Assert.Equal(invited.Id, toTrainer.IntakeId);
        Assert.True(toTrainer.EmailPending);
        var actions = await f.Db.AuditLogs.Where(a => a.EntityId == invited.Id.ToString())
            .Select(a => new { a.Action, a.UserId, a.Result }).ToListAsync();
        Assert.Contains(actions, a => a.Action == "CourseIntakeTrainerAccepted" && a.UserId == Trainer);
        var publish = Assert.Single(actions, a => a.Action == "CourseIntakePublished");
        Assert.Equal(Creator, publish.UserId);
        Assert.Equal("Published", publish.Result);
    }

    [Fact]
    public async Task An_acceptance_is_kept_when_the_intake_cannot_be_published_and_the_creator_publishes_later()
    {
        await using var f = await Fixture.CreateAsync();
        var invited = await f.Intakes.OpenClassAsync(Creator, 1, Weekly() with { SendToTrainer = true });
        await f.Db.Courses.Where(c => c.Id == 1).ExecuteUpdateAsync(set => set.SetProperty(c => c.Status, CourseStatus.PendingApproval));
        f.Db.ChangeTracker.Clear();

        var reply = await f.Invitations.RespondAsync(Trainer, invited.Id, accept: true, reason: null);

        Assert.Equal("Accepted", reply.Status);
        Assert.Equal("Draft", reply.IntakeStatus);
        var waiting = await f.Intakes.GetForCreatorAsync(Creator, invited.Id);
        Assert.Equal("Draft", waiting.Status);
        Assert.Equal("Accepted", waiting.TrainerInvitationStatus);
        Assert.Null(waiting.ConfirmedByCreatorId);
        var notice = await f.Db.Notifications.SingleAsync(n => n.UserId == Creator);
        Assert.Equal("N-intake-invitation-accepted", notice.Code);
        Assert.Contains("could not be published automatically", notice.Body);
        Assert.Contains("The Course must be published.", notice.Body);
        Assert.False(await f.Db.Notifications.AnyAsync(n => n.Code == "N-intake-published"));
        Assert.False(await f.Db.AuditLogs.AnyAsync(a => a.Action == "CourseIntakePublished"));
        Assert.True(await f.Db.AuditLogs.AnyAsync(a => a.Action == "CourseIntakeTrainerAccepted" && a.UserId == Trainer));

        await f.Db.Courses.Where(c => c.Id == 1).ExecuteUpdateAsync(set => set.SetProperty(c => c.Status, CourseStatus.Published));
        f.Db.ChangeTracker.Clear();
        var published = await f.Intakes.PublishAsync(Creator, waiting.Id, waiting.Version);

        Assert.Equal("Published", published.Status);
        Assert.Equal(Creator, published.ConfirmedByCreatorId);
    }

    [Fact]
    public async Task An_acceptance_after_registration_would_have_closed_leaves_the_intake_unchanged_for_the_creator()
    {
        await using var f = await Fixture.CreateAsync();
        var invited = await f.Intakes.OpenClassAsync(Creator, 1, Weekly() with { SendToTrainer = true });
        var opens = DateTime.UtcNow.AddDays(-2);
        var closes = DateTime.UtcNow.AddDays(-1);
        await f.Db.CourseIntakes.Where(i => i.Id == invited.Id).ExecuteUpdateAsync(set => set
            .SetProperty(i => i.RegistrationOpensAt, opens).SetProperty(i => i.RegistrationClosesAt, closes));
        f.Db.ChangeTracker.Clear();

        var reply = await f.Invitations.RespondAsync(Trainer, invited.Id, accept: true, reason: null);

        Assert.Equal("Accepted", reply.Status);
        Assert.Equal("Draft", reply.IntakeStatus);
        var notice = await f.Db.Notifications.SingleAsync(n => n.UserId == Creator);
        Assert.Contains("could not be published automatically", notice.Body);
        Assert.Contains("Registration would already be closed.", notice.Body);
        f.Db.ChangeTracker.Clear();
        var stored = await f.Db.CourseIntakes.SingleAsync(i => i.Id == invited.Id);
        Assert.Equal(CourseIntakeStatus.Draft, stored.Status);
        Assert.Equal(TrainerInvitationStatus.Accepted, stored.TrainerInvitation);
        Assert.True(stored.RegistrationOpensAt < stored.RegistrationClosesAt);
    }

    [Fact]
    public async Task The_schedule_is_locked_once_sent_and_unlocks_when_the_invitation_is_withdrawn()
    {
        await using var f = await Fixture.CreateAsync();
        var invited = await f.Intakes.OpenClassAsync(Creator, 1, Weekly() with { SendToTrainer = true });
        var extra = FirstSession.AddDays(21);
        CreateCourseSessionRequest Extra(Guid version) => new("Session 4", extra, extra.AddHours(2),
            "https://example.com/meeting", null, 0, null, version);

        var locked = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Intakes.AddSessionAsync(Creator, invited.Id, Extra(invited.Version)));
        Assert.Equal("SCHEDULE_LOCKED", locked.Code);
        var lockedDelete = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Intakes.DeleteSessionAsync(
            Creator, invited.Id, invited.Sessions[0].Id, invited.Version));
        Assert.Equal("SCHEDULE_LOCKED", lockedDelete.Code);

        var withdrawn = await f.Intakes.WithdrawTrainerInvitationAsync(Creator, invited.Id, invited.Version);
        Assert.Equal("NotSent", withdrawn.TrainerInvitationStatus);
        Assert.Contains(await f.Db.Notifications.Select(n => n.Code).ToListAsync(), code => code == "N-intake-invitation-withdrawn");

        var edited = await f.Intakes.AddSessionAsync(Creator, withdrawn.Id, Extra(withdrawn.Version));
        Assert.Equal(4, edited.Sessions.Count);
    }

    [Fact]
    public async Task A_published_intake_keeps_its_schedule_fixed()
    {
        await using var f = await Fixture.CreateAsync();
        var published = await f.PublishedAsync();

        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Intakes.DeleteSessionAsync(
            Creator, published.Id, published.Sessions[0].Id, published.Version));
        Assert.Equal("SCHEDULE_LOCKED", error.Code);
        var withdraw = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Intakes.WithdrawTrainerInvitationAsync(
            Creator, published.Id, published.Version));
        Assert.Equal("INTAKE_ALREADY_PUBLISHED", withdraw.Code);
    }

    [Fact]
    public async Task A_declined_invitation_tells_the_creator_why_and_lets_them_choose_another_trainer()
    {
        await using var f = await Fixture.CreateAsync();
        var invited = await f.Intakes.OpenClassAsync(Creator, 1, Weekly() with { SendToTrainer = true });

        var declined = await f.Invitations.RespondAsync(Trainer, invited.Id, accept: false, reason: "  I teach elsewhere on Mondays.  ");

        Assert.Equal("Declined", declined.Status);
        Assert.Equal("I teach elsewhere on Mondays.", declined.DeclineReason);
        var notice = await f.Db.Notifications.SingleAsync(n => n.UserId == Creator);
        Assert.Equal("N-intake-invitation-declined", notice.Code);
        Assert.Contains("I teach elsewhere on Mondays.", notice.Body);
        Assert.Equal($"/creator/intakes/{invited.Id}", NotificationTargetPath.For(notice.Code, notice.IntakeId));

        var current = await f.Intakes.GetForCreatorAsync(Creator, invited.Id);
        Assert.Equal("Declined", current.TrainerInvitationStatus);
        var moved = await f.Intakes.UpdateAsync(Creator, current.Id, new UpdateCourseIntakeRequest(current.RegistrationOpensAt,
            current.RegistrationClosesAt, current.StartsAt, current.EndsAt, current.Version, TrainerId: OtherTrainer));
        Assert.Equal(OtherTrainer, moved.TrainerId);
        Assert.Equal("NotSent", moved.TrainerInvitationStatus);

        var resent = await f.Intakes.SendToTrainerAsync(Creator, moved.Id, moved.Version);
        Assert.Equal("Invited", resent.TrainerInvitationStatus);
        Assert.Equal(OtherTrainer, (await f.Db.Notifications.Where(n => n.Code == "N-intake-invitation")
            .OrderByDescending(n => n.Id).FirstAsync()).UserId);
    }

    [Fact]
    public async Task The_new_trainer_must_have_accepted_the_course()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Intakes.OpenClassAsync(Creator, 1, Weekly());

        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Intakes.UpdateAsync(Creator, draft.Id,
            new UpdateCourseIntakeRequest(draft.RegistrationOpensAt, draft.RegistrationClosesAt, draft.StartsAt, draft.EndsAt,
                draft.Version, TrainerId: 3)));

        Assert.Equal("TRAINER_NOT_ACCEPTED", error.Code);
    }

    [Fact]
    public async Task Trainers_see_every_session_and_any_clash_with_teaching_they_already_committed_to()
    {
        await using var f = await Fixture.CreateAsync();
        await f.PublishedAsync(course: 2);
        var invited = await f.Intakes.OpenClassAsync(Creator, 1, Weekly() with
        {
            SessionStarts = [FirstSession.AddHours(1), FirstSession.AddDays(3)], SendToTrainer = true,
        });

        var list = await f.Invitations.ListForTrainerAsync(Trainer);

        var invitation = Assert.Single(list, item => item.IntakeId == invited.Id);
        Assert.Equal("Invited", invitation.Status);
        Assert.Equal("C1", invitation.CourseCode);
        Assert.Equal("User 1", invitation.CreatorName);
        Assert.Equal(2, invitation.Sessions.Count);
        var clash = Assert.Single(invitation.Sessions[0].Clashes);
        Assert.Equal("C2", clash.CourseCode);
        Assert.Equal(FirstSession, clash.StartsAt);
        Assert.Empty(invitation.Sessions[1].Clashes);
        Assert.Empty(await f.Invitations.ListForTrainerAsync(OtherTrainer));
    }

    [Fact]
    public async Task Replies_are_safe_to_repeat_and_only_the_invited_trainer_can_reply()
    {
        await using var f = await Fixture.CreateAsync();
        var invited = await f.Intakes.OpenClassAsync(Creator, 1, Weekly() with { SendToTrainer = true });

        var stranger = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Invitations.RespondAsync(OtherTrainer, invited.Id, true, null));
        Assert.Equal("INVITATION_NOT_FOUND", stranger.Code);

        await f.Invitations.RespondAsync(Trainer, invited.Id, true, null);
        var again = await f.Invitations.RespondAsync(Trainer, invited.Id, true, null);
        Assert.Equal("Accepted", again.Status);
        Assert.Equal(1, await f.Db.Notifications.CountAsync(n => n.Code == "N-intake-invitation-accepted"));

        var change = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Invitations.RespondAsync(Trainer, invited.Id, false, null));
        Assert.Equal("INVITATION_NOT_PENDING", change.Code);
    }

    [Fact]
    public async Task Each_invitation_step_is_audited()
    {
        await using var f = await Fixture.CreateAsync();
        var invited = await f.Intakes.OpenClassAsync(Creator, 1, Weekly() with { SendToTrainer = true });
        await f.Invitations.RespondAsync(Trainer, invited.Id, false, "Busy");

        var actions = await f.Db.AuditLogs.Where(a => a.EntityId == invited.Id.ToString()).Select(a => new { a.Action, a.UserId }).ToListAsync();

        Assert.Contains(actions, a => a.Action == "CourseIntakeTrainerInvited" && a.UserId == Creator);
        Assert.Contains(actions, a => a.Action == "CourseIntakeTrainerDeclined" && a.UserId == Trainer);
    }

    [Fact]
    public async Task Upgrading_an_existing_database_marks_running_intakes_as_accepted()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Intakes.OpenClassAsync(Creator, 1, Weekly());
        var published = await f.PublishedAsync(course: 2);
        await f.Db.Database.ExecuteSqlRawAsync("UPDATE CourseIntakes SET TrainerInvitation = 0");

        await IntakeTrainerInvitationSchema.EnsureAsync(f.Db);
        await IntakeTrainerInvitationSchema.EnsureAsync(f.Db);

        f.Db.ChangeTracker.Clear();
        Assert.Equal(TrainerInvitationStatus.NotSent, (await f.Db.CourseIntakes.SingleAsync(i => i.Id == draft.Id)).TrainerInvitation);
        Assert.Equal(TrainerInvitationStatus.Accepted, (await f.Db.CourseIntakes.SingleAsync(i => i.Id == published.Id)).TrainerInvitation);
    }

    private sealed class Fixture(SqliteConnection connection, CoLearnXDbContext db) : IAsyncDisposable
    {
        public CoLearnXDbContext Db { get; } = db;
        public CourseIntakeService Intakes { get; } = new(db);
        public IntakeInvitationService Invitations { get; } = new(db);

        public async Task<CourseIntakeDetailDto> PublishedAsync(int course = 1)
        {
            var draft = await Intakes.OpenClassAsync(Creator, course, Weekly() with { SendToTrainer = true });
            // Accepting publishes the Intake; no separate publish step is needed.
            await Invitations.RespondAsync(Trainer, draft.Id, accept: true, reason: null);
            var published = await Intakes.GetForCreatorAsync(Creator, draft.Id);
            Assert.Equal("Published", published.Status);
            return published;
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(Enumerable.Range(1, 5).Select(id => new User { Id = id, Email = $"invite-{id}@example.com", FullName = $"User {id}",
                IsActive = true, Roles = [new UserRole { Role = id switch { 1 => AppRole.Creator, 3 => AppRole.Member, _ => AppRole.Trainer } }] }));
            foreach (var id in new[] { 1, 2 })
            {
                db.Courses.Add(new Course { Id = id, Code = $"C{id}", Title = $"Course {id}", TrainerId = Trainer, CreatorId = Creator,
                    Status = CourseStatus.Published, CreditCost = 20 });
                foreach (var trainer in new[] { Trainer, OtherTrainer })
                    db.CourseTrainers.Add(new CourseTrainer { CourseId = id, TrainerId = trainer, Status = CourseTrainerStatus.Accepted,
                        InvitedByUserId = Creator, InvitedAt = DateTime.UtcNow, RespondedAt = DateTime.UtcNow });
            }
            await db.SaveChangesAsync();
            return new Fixture(connection, db);
        }

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
