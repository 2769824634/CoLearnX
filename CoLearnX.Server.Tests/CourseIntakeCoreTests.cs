using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CoLearnX.Server.Tests;

public class CourseIntakeCoreTests
{
    // User 1 creates the course; user 5 teaches it.
    private const int Trainer = 5;
    private static readonly DateTime Start = new(2027, 1, 10, 9, 0, 0, DateTimeKind.Utc);
    private static CreateCourseIntakeRequest DraftRequest => new(Start.AddDays(-20), Start.AddDays(-10), Start, Start.AddDays(2), TrainerId: Trainer);
    private static CreateCourseSessionRequest SessionRequest(Guid version) => new("Session 1", Start, Start.AddHours(1),
        "https://example.com/meeting", null, 0, null, version);

    [Fact]
    public async Task MinimumEnrollmentRulesRejectLateClosingAndUndersizedPhysicalRoom()
    {
        await using var f = await Fixture.CreateAsync();
        var late = DraftRequest with { RegistrationClosesAt = Start.AddDays(-9), DaysBeforeTeaching = 10 };
        Assert.Equal("INVALID_INTAKE_DATES", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.CreateAsync(1, 1, late))).Code);
        var draft = await f.Service.CreateAsync(1, 1, DraftRequest);
        var smallRoom = SessionRequest(draft.Version) with
        {
            MeetingLink = null, PhysicalAddress = "Room 1", PhysicalCapacity = 9,
            PhysicalBookingDeadline = Start.AddDays(-11),
        };
        Assert.Equal("CAPACITY_BELOW_MIN_ENROLLMENT", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.AddSessionAsync(1, draft.Id, smallRoom))).Code);
        Assert.Empty(await f.Db.CourseSessions.ToListAsync());
    }

    [Fact]
    public async Task DraftToPending_PersistsSingleHierarchy_AndUserAudit()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Service.CreateAsync(1, 1, DraftRequest);
        Assert.Equal("Draft", draft.Status);
        Assert.Empty(draft.Sessions);
        Assert.Equal("SESSION_REQUIRED", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.PublishAsync(1, draft.Id, draft.Version))).Code);
        var scheduled = await f.Service.AddSessionAsync(1, draft.Id, SessionRequest(draft.Version));
        Assert.NotEqual(draft.Version, scheduled.Version);
        await f.TrainerAcceptsAsync(draft.Id);
        var pending = await f.Service.PublishAsync(1, draft.Id, scheduled.Version);
        Assert.Equal("Published", pending.Status);
        Assert.NotNull(pending.SubmittedAt);
        Assert.Equal(1, pending.ConfirmedByCreatorId);
        Assert.Equal(pending.Version, (await f.Service.PublishAsync(1, draft.Id, pending.Version)).Version);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Service.GetForCreatorAsync(1, draft.Id);
        Assert.Equal(DateTimeKind.Utc, saved.StartsAt.Kind);
        Assert.Equal(DateTimeKind.Utc, saved.Sessions[0].StartsAt.Kind);
        Assert.Equal(draft.Id, Assert.Single(saved.Sessions).CourseIntakeId);
        Assert.Equal(1, await f.Db.Courses.CountAsync());
        Assert.Equal(1, await f.Db.CourseIntakes.CountAsync());
        var logs = await f.Db.AuditLogs.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(3, logs.Count);
        Assert.All(logs, x => { Assert.Equal(1, x.UserId); Assert.Null(x.AdminAccountId); Assert.Equal(draft.Id.ToString(), x.EntityId); });
        Assert.Equal("CourseIntakePublished", logs[^1].Action);
    }

    [Theory]
    [InlineData(0, "UNAUTHENTICATED")]
    [InlineData(3, "CREATOR_REQUIRED")]
    [InlineData(4, "CREATOR_REQUIRED")]
    public async Task Create_RequiresAuthenticatedActiveTrainer(int actor, string code)
    {
        await using var f = await Fixture.CreateAsync();
        Assert.Equal(code, (await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.CreateAsync(actor, 1, DraftRequest))).Code);
        Assert.Empty(await f.Db.CourseIntakes.ToListAsync());
    }

    [Theory]
    [InlineData(CourseStatus.Draft)]
    [InlineData(CourseStatus.PendingApproval)]
    [InlineData(CourseStatus.Archived)]
    [InlineData(CourseStatus.Rejected)]
    public async Task Create_RejectsUnpublishedCourse(CourseStatus status)
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.Courses.SingleAsync()).Status = status;
        await f.Db.SaveChangesAsync();
        Assert.Equal("COURSE_NOT_PUBLISHED", (await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.CreateAsync(1, 1, DraftRequest))).Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task InvalidDates_AreRejectedWithoutWrites(int scenario)
    {
        await using var f = await Fixture.CreateAsync();
        var r = scenario switch
        {
            0 => DraftRequest with { RegistrationOpensAt = DraftRequest.RegistrationClosesAt },
            1 => DraftRequest with { RegistrationClosesAt = Start.AddMinutes(1) },
            2 => DraftRequest with { EndsAt = Start },
            _ => DraftRequest with { StartsAt = DateTime.SpecifyKind(Start, DateTimeKind.Unspecified) },
        };
        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.CreateAsync(1, 1, r));
        Assert.Equal(400, error.StatusCode);
        Assert.NotEmpty(error.FieldErrors);
        if (scenario == 0)
        {
            Assert.Equal("INVALID_INTAKE_DATES", error.Code);
            Assert.True(error.FieldErrors.ContainsKey("registrationClosesAt"));
            Assert.Contains("close after it opens", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        else if (scenario == 1)
        {
            Assert.Equal("INVALID_INTAKE_DATES", error.Code);
            Assert.True(error.FieldErrors.ContainsKey("registrationClosesAt"));
            Assert.Contains("on or before", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        else if (scenario == 2)
        {
            Assert.Equal("INVALID_INTAKE_DATES", error.Code);
            Assert.True(error.FieldErrors.ContainsKey("endsAt"));
            Assert.Contains("end after it starts", error.Message, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Empty(await f.Db.CourseIntakes.ToListAsync());
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task RegistrationCannotStartInThePast_AndLeadTimeIsChosenByTheCreator()
    {
        await using var f = await Fixture.CreateAsync();
        var past = DraftRequest with
        {
            RegistrationOpensAt = DateTime.UtcNow.AddMinutes(-2),
            RegistrationClosesAt = DateTime.UtcNow.AddDays(2),
            StartsAt = DateTime.UtcNow.AddDays(4),
            EndsAt = DateTime.UtcNow.AddDays(6),
        };
        var pastError = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.CreateAsync(1, 1, past));
        Assert.Equal("REGISTRATION_IN_THE_PAST", pastError.Code);
        Assert.True(pastError.FieldErrors.ContainsKey("registrationOpensAt"));

        var shortGap = DraftRequest with { DaysBeforeTeaching = 11 };
        var gapError = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.CreateAsync(1, 1, shortGap));
        Assert.Equal("INVALID_INTAKE_DATES", gapError.Code);
        Assert.Contains("11 days", gapError.Message, StringComparison.OrdinalIgnoreCase);

        var chosen = DraftRequest with { RegistrationClosesAt = Start.AddDays(-3), DaysBeforeTeaching = 3 };
        var draft = await f.Service.CreateAsync(1, 1, chosen);
        Assert.Equal("Draft", draft.Status);
    }

    [Fact]
    public async Task Ownership_FiltersListsAndEveryMutatingCommand()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        Assert.Equal("CREATOR_REQUIRED", (await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.CreateAsync(2, 1, DraftRequest))).Code);
        Assert.Equal(d.Id, Assert.Single(await f.Service.ListOwnedAsync(Trainer)).Id);
        Assert.Equal("INTAKE_NOT_FOUND", (await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.GetOwnedAsync(2, d.Id))).Code);
        var commands = new Func<Task>[] {
            async () => await f.Service.UpdateAsync(2, d.Id, new(DraftRequest.RegistrationOpensAt, DraftRequest.RegistrationClosesAt, Start, Start.AddDays(2), d.Version)),
            async () => await f.Service.AddSessionAsync(2, d.Id, SessionRequest(d.Version)),
            async () => await f.Service.UpdateSessionAsync(2, d.Id, 1, new("Changed", Start, Start.AddHours(1), "https://example.com", null, 0, null, d.Version)),
            async () => await f.Service.DeleteSessionAsync(2, d.Id, 1, d.Version),
            async () => await f.Service.PublishAsync(2, d.Id, d.Version),
        };
        foreach (var command in commands)
            Assert.Equal("CREATOR_REQUIRED", (await Assert.ThrowsAsync<CourseIntakeException>(command)).Code);
        Assert.Equal(Trainer, d.TrainerId);
        Assert.Equal(1, await f.Db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task InvalidSession_LeavesVersionAndStructureUnchanged(int scenario)
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        var r = SessionRequest(d.Version);
        r = scenario switch {
            0 => r with { StartsAt = Start.AddDays(-11), EndsAt = Start.AddDays(-11).AddHours(1) }, // before registration closes
            1 => r with { EndsAt = Start },
            2 => r with { MeetingLink = "javascript:alert(1)" },
            3 => r with { PhysicalCapacity = -1 },
            4 => r with { PhysicalAddress = "Room 1", PhysicalCapacity = 10, PhysicalBookingDeadline = Start.AddMinutes(1) },
            _ => r with { MeetingLink = null },
        };
        await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.AddSessionAsync(1, d.Id, r));
        Assert.Equal(d.Version, (await f.Service.GetForCreatorAsync(1, d.Id)).Version);
        Assert.Empty(await f.Db.CourseSessions.ToListAsync());
    }

    [Fact]
    public async Task SessionMaintenance_AndDateShrink_RespectAggregateBounds()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        d = await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        var id = Assert.Single(d.Sessions).Id;
        d = await f.Service.UpdateSessionAsync(1, d.Id, id,
            new("Room session", Start, Start.AddHours(2), null, "Room 10", 20, Start.AddDays(-1), d.Version));
        Assert.Equal(20, Assert.Single(d.Sessions).PhysicalCapacity);
        Assert.Equal("SESSION_OUTSIDE_INTAKE", (await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.UpdateAsync(1, d.Id,
            new(DraftRequest.RegistrationOpensAt, DraftRequest.RegistrationClosesAt, Start, Start.AddHours(1), d.Version)))).Code);
        d = await f.Service.DeleteSessionAsync(1, d.Id, id, d.Version);
        Assert.Empty(d.Sessions);
        Assert.Empty(await f.Db.CourseSessions.ToListAsync());
    }

    [Theory]
    [InlineData(CourseIntakeStatus.PendingApproval)]
    [InlineData(CourseIntakeStatus.Completed)]
    [InlineData(CourseIntakeStatus.Cancelled)]
    public async Task LockedStates_BlockStructuralCommands(CourseIntakeStatus status)
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        (await f.Db.CourseIntakes.SingleAsync()).Status = status; // state fixture, not Creator workflow
        await f.Db.SaveChangesAsync();
        Assert.Equal("INTAKE_NOT_EDITABLE", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version)))).Code);
    }

    [Fact]
    public async Task RejectedAmendment_ReturnsToDraft_AndCanResubmit()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        var entity = await f.Db.CourseIntakes.SingleAsync();
        entity.Status = CourseIntakeStatus.Rejected;
        entity.ConfirmationNote = "Fixture rejection";
        await f.Db.SaveChangesAsync();
        d = await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        Assert.Equal("Draft", d.Status);
        Assert.Null(d.ConfirmationNote);
        await f.TrainerAcceptsAsync(d.Id);
        Assert.Equal("Published", (await f.Service.PublishAsync(1, d.Id, d.Version)).Status);
    }

    [Fact]
    public async Task ConcurrentSessionEdits_RollBackLosingSessionAndAudit()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        await using var secondDb = new CoLearnXDbContext(f.Options);
        var second = new CourseIntakeService(secondDb);
        await second.GetForCreatorAsync(1, d.Id); // read the same original version before the winning edit
        await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => second.AddSessionAsync(1, d.Id, SessionRequest(d.Version)));
        Assert.Equal("INTAKE_VERSION_CONFLICT", error.Code);
        Assert.Equal(1, await f.Db.CourseSessions.CountAsync());
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync());
        Assert.Equal("INTAKE_VERSION_CONFLICT", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.PublishAsync(1, d.Id, d.Version))).Code);
    }

    [Fact]
    public async Task ReferencedSession_CannotBeDeleted_ByServiceOrDatabase()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        d = await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        var id = Assert.Single(d.Sessions).Id;
        f.Db.AttendanceRecords.Add(new AttendanceRecord { CourseSessionId = id, UserId = 3, RecordedByTrainerId = Trainer });
        await f.Db.SaveChangesAsync();
        Assert.Equal("SESSION_HAS_HISTORY", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.DeleteSessionAsync(1, d.Id, id, d.Version))).Code);
        f.Db.ChangeTracker.Clear();
        f.Db.CourseSessions.Remove(await f.Db.CourseSessions.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task SessionWithUploadedMaterial_CannotBeDeleted_AndMaterialRowIsPreserved()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Service.CreateAsync(1, 1, DraftRequest);
        draft = await f.Service.AddSessionAsync(1, draft.Id, SessionRequest(draft.Version));
        var sessionId = Assert.Single(draft.Sessions).Id;
        f.Db.SessionMaterials.Add(new SessionMaterial
        {
            CourseSessionId = sessionId,
            AddedByTrainerId = Trainer,
            Title = "Uploaded handout",
            FilePath = $"session-materials/1/{sessionId}/handout.pdf",
            Format = "PDF",
        });
        await f.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<CourseIntakeException>(() =>
            f.Service.DeleteSessionAsync(1, draft.Id, sessionId, draft.Version));

        Assert.Equal("SESSION_HAS_MATERIALS", error.Code);
        Assert.True(await f.Db.CourseSessions.AnyAsync(item => item.Id == sessionId));
        Assert.True(await f.Db.SessionMaterials.AnyAsync(item => item.CourseSessionId == sessionId));
    }

    [Fact]
    public async Task LegacyCatalogAndEnrolment_DoNotExposeDraftSessions()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        d = await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        var detail = await new CourseService(f.Db).GetByIdAsync(1, 3);
        Assert.Empty(detail!.Sessions);
        Assert.Equal("REGISTRATION_CLOSED", (await Assert.ThrowsAsync<CourseException>(() => new EnrollmentService(f.Db).EnrolAsync(3,
            new EnrolRequest(1, Assert.Single(d.Sessions).Id)))).Code);
        Assert.Empty(await f.Db.Enrollments.ToListAsync());
        Assert.Empty(await f.Db.CreditTransactions.ToListAsync());
        Assert.Equal(100, (await f.Db.Users.FindAsync(3))!.CreditBalance);
    }

    [Fact]
    public async Task LegacyPublishedSession_StillEnrolsOnceWithMatchingCreditLedger()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        d = await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version) with {
            PhysicalAddress = "Room 1", PhysicalCapacity = 10, PhysicalBookingDeadline = Start.AddDays(-1) });
        (await f.Db.CourseIntakes.SingleAsync()).Status = CourseIntakeStatus.Published; // compatibility fixture only
        (await f.Db.CourseIntakes.SingleAsync()).RegistrationOpensAt = DateTime.UtcNow.AddDays(-1);
        await f.Db.SaveChangesAsync();
        Assert.Single((await new CourseService(f.Db).GetByIdAsync(1, 3))!.Sessions);
        var service = new EnrollmentService(f.Db);
        var request = new EnrolRequest(1, Assert.Single(d.Sessions).Id);
        var result = await service.EnrolAsync(3, request);
        Assert.Equal(80, result.BalanceAfter);
        Assert.Equal("ALREADY_ENROLLED", (await Assert.ThrowsAsync<CourseException>(() => service.EnrolAsync(3, request))).Code);
        Assert.Equal(1, await f.Db.Enrollments.CountAsync());
        var ledger = await f.Db.CreditTransactions.SingleAsync();
        Assert.Equal(CreditTransactionType.Hold, ledger.Type);
        Assert.Equal(EnrollmentStatus.Reserved, (await f.Db.Enrollments.SingleAsync()).Status);
        Assert.Equal(-20, ledger.Delta);
        Assert.Equal(80, ledger.BalanceAfter);
        Assert.Equal(1, (await f.Db.CourseSessions.AsNoTracking().SingleAsync()).SeatsTaken);
    }

    [Fact]
    public async Task CreatorOrTrainer_CannotEnrolInOwnClass()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        d = await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        var intake = await f.Db.CourseIntakes.SingleAsync();
        intake.Status = CourseIntakeStatus.Published;
        intake.RegistrationOpensAt = DateTime.UtcNow.AddDays(-1);
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<CourseException>(() => new EnrollmentService(f.Db).EnrolAsync(1,
            new EnrolRequest(1, Assert.Single(d.Sessions).Id)));
        Assert.Equal("SELF_ENROLLMENT_NOT_ALLOWED", error.Code);
        Assert.Empty(await f.Db.Enrollments.ToListAsync());
    }

    [Fact]
    public async Task Submission_RechecksCoursePublication()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        d = await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        (await f.Db.Courses.SingleAsync()).Status = CourseStatus.Archived;
        await f.Db.SaveChangesAsync();
        Assert.Equal("COURSE_NOT_PUBLISHED", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.PublishAsync(1, d.Id, d.Version))).Code);
        Assert.Equal("Draft", (await f.Service.GetForCreatorAsync(1, d.Id)).Status);
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Database_RejectsOrphanSessionsAndInvalidIntakeDates()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.CourseSessions.Add(new CourseSession { CourseIntakeId = 999, StartsAt = Start, EndsAt = Start.AddHours(1) });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();
        f.Db.CourseIntakes.Add(new CourseIntake { CourseId = 1, TrainerId = Trainer, RegistrationOpensAt = Start,
            RegistrationClosesAt = Start, StartsAt = Start, EndsAt = Start });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditFailure_RollsBackCreateAndSessionMutation()
    {
        await using var f = await Fixture.CreateAsync();
        await using (var failing = new FailingAuditDb(f.Options))
            await Assert.ThrowsAsync<DbUpdateException>(() => new CourseIntakeService(failing).CreateAsync(1, 1, DraftRequest));
        Assert.Empty(await f.Db.CourseIntakes.ToListAsync());
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        await using (var failing = new FailingAuditDb(f.Options))
            await Assert.ThrowsAsync<DbUpdateException>(() => new CourseIntakeService(failing).AddSessionAsync(1, d.Id, SessionRequest(d.Version)));
        Assert.Empty(await f.Db.CourseSessions.ToListAsync());
        Assert.Equal(1, await f.Db.AuditLogs.CountAsync());
        Assert.Equal(d.Version, (await f.Db.CourseIntakes.AsNoTracking().SingleAsync()).Version);
    }

    [Fact]
    public async Task OldSchema_IsRejectedBeforeAnySeedWrites()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE CourseSessions (Id INTEGER PRIMARY KEY, CourseId INTEGER)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO CourseSessions VALUES (42, 7)");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedData.InitializeAsync(db));
        Assert.Contains("fresh isolated database", error.Message);
        Assert.Equal(42, await db.Database.SqlQueryRaw<int>("SELECT Id AS Value FROM CourseSessions").SingleAsync());
    }

    private sealed class FailingAuditDb(DbContextOptions<CoLearnXDbContext> options) : CoLearnXDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added))
                throw new DbUpdateException("Injected audit failure");
            return base.SaveChangesAsync(ct);
        }
    }

    [Fact]
    public async Task Publish_opens_registration_now_when_the_planned_opening_time_has_passed()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Service.CreateAsync(1, 1, DraftRequest);
        draft = await f.Service.AddSessionAsync(1, draft.Id, SessionRequest(draft.Version));
        // The Creator prepared the draft earlier; the planned opening time is now behind us.
        var stored = await f.Db.CourseIntakes.SingleAsync(x => x.Id == draft.Id);
        stored.RegistrationOpensAt = DateTime.UtcNow.AddHours(-1);
        stored.RegistrationClosesAt = DateTime.UtcNow.AddDays(3);
        await f.Db.SaveChangesAsync();

        await f.TrainerAcceptsAsync(draft.Id);
        var before = DateTime.UtcNow;
        var published = await f.Service.PublishAsync(1, draft.Id, draft.Version);

        Assert.Equal("Published", published.Status);
        Assert.InRange(published.RegistrationOpensAt, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Publish_is_refused_when_registration_would_already_be_closed()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Service.CreateAsync(1, 1, DraftRequest);
        draft = await f.Service.AddSessionAsync(1, draft.Id, SessionRequest(draft.Version));
        var stored = await f.Db.CourseIntakes.SingleAsync(x => x.Id == draft.Id);
        stored.RegistrationOpensAt = DateTime.UtcNow.AddDays(-3);
        stored.RegistrationClosesAt = DateTime.UtcNow.AddMinutes(-5);
        await f.Db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.PublishAsync(1, draft.Id, draft.Version));

        Assert.Equal("REGISTRATION_ALREADY_CLOSED", error.Code);
        Assert.True(error.FieldErrors.ContainsKey("registrationClosesAt"));
    }

    private static DateTime FirstClass => DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(10).AddHours(8), DateTimeKind.Utc);
    private static OpenClassRequest WeeklyClass => new(TrainerId: Trainer,
        SessionStarts: [FirstClass, FirstClass.AddDays(7), FirstClass.AddDays(14)],
        MeetingLink: "https://example.com/meeting");

    [Fact]
    public async Task Publishing_requires_total_session_time_to_cover_the_course_duration()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.Courses.SingleAsync()).DurationHours = 3;
        await f.Db.SaveChangesAsync();
        var draft = await f.Service.CreateAsync(1, 1, DraftRequest);
        draft = await f.Service.AddSessionAsync(1, draft.Id, SessionRequest(draft.Version));
        draft = await f.Service.AddSessionAsync(1, draft.Id, SessionRequest(draft.Version) with
        {
            Label = "Session 2", StartsAt = Start.AddDays(1), EndsAt = Start.AddDays(1).AddHours(1),
        });
        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.PublishAsync(1, draft.Id, draft.Version));
        Assert.Equal("SESSION_DURATION_INSUFFICIENT", error.Code);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(CourseIntakeStatus.Draft, (await f.Db.CourseIntakes.SingleAsync()).Status);
        draft = await f.Service.AddSessionAsync(1, draft.Id, SessionRequest(draft.Version) with
        {
            Label = "Session 3", StartsAt = Start.AddDays(2), EndsAt = Start.AddDays(2).AddHours(1),
        });
        await f.TrainerAcceptsAsync(draft.Id);
        Assert.Equal("Published", (await f.Service.PublishAsync(1, draft.Id, draft.Version)).Status);
    }

    [Fact]
    public async Task Opening_a_short_class_fails_without_creating_an_orphan_draft()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.Courses.SingleAsync()).DurationHours = 7;
        await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.OpenClassAsync(1, 1, WeeklyClass with { SendToTrainer = true }));
        Assert.Equal("SESSION_DURATION_INSUFFICIENT", error.Code);
        Assert.Empty(await f.Db.CourseIntakes.ToListAsync());
    }

    [Fact]
    public async Task A_published_intake_keeps_every_session_the_trainer_accepted()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Db.Courses.SingleAsync()).DurationHours = 6;
        await f.Db.SaveChangesAsync();
        var opened = await f.Service.OpenClassAsync(1, 1, WeeklyClass);
        await f.TrainerAcceptsAsync(opened.Id);
        opened = await f.Service.PublishAsync(1, opened.Id, opened.Version);
        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.DeleteSessionAsync(
            1, opened.Id, opened.Sessions[0].Id, opened.Version));
        Assert.Equal("SCHEDULE_LOCKED", error.Code);
        Assert.Equal(3, await f.Db.CourseSessions.CountAsync());
    }

    [Fact]
    public async Task Open_class_creates_every_session_and_sends_the_schedule_to_the_trainer_in_one_step()
    {
        await using var f = await Fixture.CreateAsync();
        var before = DateTime.UtcNow;

        var opened = await f.Service.OpenClassAsync(1, 1, WeeklyClass with { SendToTrainer = true });

        Assert.Equal("Draft", opened.Status);
        Assert.Equal("Invited", opened.TrainerInvitationStatus);
        Assert.Equal(3, opened.MinEnrollment);
        Assert.Equal(["Session 1", "Session 2", "Session 3"], opened.Sessions.OrderBy(x => x.StartsAt).Select(x => x.Label));
        Assert.All(opened.Sessions, session => Assert.Equal(TimeSpan.FromHours(2), session.EndsAt - session.StartsAt));
        Assert.All(opened.Sessions, session => Assert.Equal("https://example.com/meeting", session.MeetingLink));
        // Registration closes two days before the first session; it opens again when the Intake is published.
        Assert.InRange(opened.RegistrationOpensAt, before.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
        Assert.Equal(FirstClass.AddDays(-2), opened.RegistrationClosesAt);
        Assert.Equal(FirstClass, opened.StartsAt);
        Assert.Equal(FirstClass.AddDays(14).AddHours(2), opened.EndsAt);
    }

    [Fact]
    public async Task Open_class_can_stay_a_draft()
    {
        await using var f = await Fixture.CreateAsync();

        var draft = await f.Service.OpenClassAsync(1, 1, WeeklyClass with { Publish = false, MinEnrollment = 5 });

        Assert.Equal("Draft", draft.Status);
        Assert.Equal(3, draft.Sessions.Count);
        Assert.Equal(5, draft.MinEnrollment);
    }

    [Fact]
    public async Task Open_class_says_when_the_first_session_is_too_soon_for_the_registration_window()
    {
        await using var f = await Fixture.CreateAsync();
        var tomorrow = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(1), DateTimeKind.Utc);

        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.OpenClassAsync(1, 1,
            WeeklyClass with { SessionStarts = [tomorrow], RegistrationClosesDaysBefore = 2 }));

        Assert.Equal("FIRST_SESSION_TOO_SOON", error.Code);
        Assert.True(error.FieldErrors.ContainsKey("sessionStarts"));
        Assert.False(await f.Db.CourseIntakes.AnyAsync());
    }

    [Fact]
    public async Task Open_class_needs_a_place_to_meet_and_sessions_in_order()
    {
        await using var f = await Fixture.CreateAsync();

        var noPlace = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.OpenClassAsync(1, 1,
            WeeklyClass with { MeetingLink = null }));
        Assert.Equal("DELIVERY_LOCATION_REQUIRED", noPlace.Code);

        var overlapping = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.OpenClassAsync(1, 1,
            WeeklyClass with { SessionStarts = [FirstClass, FirstClass.AddHours(1)] }));
        Assert.Equal("INVALID_SESSION_SCHEDULE", overlapping.Code);

        Assert.False(await f.Db.CourseIntakes.AnyAsync());
    }

    [Fact]
    public async Task Open_class_rejects_a_bad_meeting_link_before_creating_a_draft()
    {
        await using var f = await Fixture.CreateAsync();

        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.OpenClassAsync(1, 1,
            WeeklyClass with { MeetingLink = "javascript:alert(1)" }));

        Assert.Equal("INVALID_MEETING_LINK", error.Code);
        Assert.False(await f.Db.CourseIntakes.AnyAsync());
    }

    [Fact]
    public async Task Adding_a_session_outside_the_teaching_dates_extends_them()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Service.OpenClassAsync(1, 1, WeeklyClass with { SessionStarts = [FirstClass], Publish = false });

        var later = FirstClass.AddDays(7);
        draft = await f.Service.AddSessionAsync(1, draft.Id,
            new("Session 2", later, later.AddHours(2), "https://example.com/meeting", null, 0, null, draft.Version));
        Assert.Equal(later.AddHours(2), draft.EndsAt);

        var earlier = FirstClass.AddHours(-12);
        draft = await f.Service.AddSessionAsync(1, draft.Id,
            new("Warm-up", earlier, earlier.AddHours(1), "https://example.com/meeting", null, 0, null, draft.Version));
        Assert.Equal(earlier, draft.StartsAt);
        Assert.Equal(3, draft.Sessions.Count);
    }

    [Fact]
    public async Task A_session_cannot_start_before_registration_closes()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Service.OpenClassAsync(1, 1, WeeklyClass with { SessionStarts = [FirstClass], Publish = false });
        var tooEarly = draft.RegistrationClosesAt.AddHours(-1);

        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.AddSessionAsync(1, draft.Id,
            new("Too early", tooEarly, tooEarly.AddHours(1), "https://example.com/meeting", null, 0, null, draft.Version)));

        Assert.Equal("SESSION_BEFORE_REGISTRATION_CLOSES", error.Code);
        Assert.Equal(FirstClass, (await f.Service.GetForCreatorAsync(1, draft.Id)).StartsAt);
    }

    [Fact]
    public async Task Deleting_or_moving_a_session_refits_the_teaching_dates()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Service.OpenClassAsync(1, 1, WeeklyClass with { Publish = false });
        var ordered = draft.Sessions.OrderBy(session => session.StartsAt).ToList();

        draft = await f.Service.DeleteSessionAsync(1, draft.Id, ordered[^1].Id, draft.Version);

        Assert.Equal(2, draft.Sessions.Count);
        Assert.Equal(FirstClass, draft.StartsAt);
        Assert.Equal(FirstClass.AddDays(7).AddHours(2), draft.EndsAt);

        var first = draft.Sessions.OrderBy(session => session.StartsAt).First();
        var moved = FirstClass.AddDays(1);
        draft = await f.Service.UpdateSessionAsync(1, draft.Id, first.Id,
            new("Session 1", moved, moved.AddHours(2), "https://example.com/meeting", null, 0, null, draft.Version));

        Assert.Equal(moved, draft.StartsAt);
        Assert.Equal(FirstClass.AddDays(7).AddHours(2), draft.EndsAt);
    }

    [Fact]
    public async Task The_schedule_can_still_be_edited_after_registration_has_opened()
    {
        await using var f = await Fixture.CreateAsync();
        var open = await f.Service.OpenClassAsync(1, 1, WeeklyClass);
        var stored = await f.Db.CourseIntakes.SingleAsync(x => x.Id == open.Id);
        stored.RegistrationOpensAt = DateTime.UtcNow.AddHours(-1);
        await f.Db.SaveChangesAsync();
        open = await f.Service.GetForCreatorAsync(1, open.Id);

        // Registration opened a moment ago; the Creator moves the closing time and the minimum.
        var edited = await f.Service.UpdateAsync(1, open.Id, new(open.RegistrationOpensAt, FirstClass.AddDays(-1),
            open.StartsAt, open.EndsAt, open.Version, MinEnrollment: 4));
        Assert.Equal(FirstClass.AddDays(-1), edited.RegistrationClosesAt);
        Assert.Equal(4, edited.MinEnrollment);

        // Moving the opening time into the past is still refused.
        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.UpdateAsync(1, open.Id,
            new(open.RegistrationOpensAt.AddHours(-3), FirstClass.AddDays(-1), open.StartsAt, open.EndsAt, edited.Version)));
        Assert.Equal("REGISTRATION_IN_THE_PAST", error.Code);
    }

    private sealed class Fixture(SqliteConnection connection, CoLearnXDbContext db, DbContextOptions<CoLearnXDbContext> options) : IAsyncDisposable
    {
        public CoLearnXDbContext Db { get; } = db;
        public DbContextOptions<CoLearnXDbContext> Options { get; } = options;
        public CourseIntakeService Service { get; } = new(db);
        // State fixture: the Trainer accepted the schedule. The real reply is covered by IntakeTrainerInvitationTests.
        public async Task TrainerAcceptsAsync(int intakeId)
        {
            await Db.CourseIntakes.Where(i => i.Id == intakeId)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.TrainerInvitation, TrainerInvitationStatus.Accepted));
            Db.ChangeTracker.Clear();
        }
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options;
            var db = new CoLearnXDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(Enumerable.Range(1, 5).Select(id => new User { Id = id, Email = $"b1-{id}@example.com", FullName = $"User {id}",
                CreditBalance = 100, IsActive = id != 4,
                Roles = [new UserRole { Role = id switch { 1 => AppRole.Creator, 3 => AppRole.Member, _ => AppRole.Trainer } }] }));
            db.Courses.Add(new Course { Id = 1, Code = "B1", Title = "B1 Test", TrainerId = Trainer, CreatorId = 1, Status = CourseStatus.Published, CreditCost = 20 });
            db.CourseTrainers.Add(new CourseTrainer { CourseId = 1, TrainerId = Trainer, Status = CourseTrainerStatus.Accepted, InvitedByUserId = 1, InvitedAt = DateTime.UtcNow, RespondedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return new Fixture(connection, db, options);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
