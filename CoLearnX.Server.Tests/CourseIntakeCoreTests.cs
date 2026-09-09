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
    private static readonly DateTime Start = new(2027, 1, 10, 9, 0, 0, DateTimeKind.Utc);
    private static CreateCourseIntakeRequest DraftRequest => new(Start.AddDays(-10), Start.AddDays(-1), Start, Start.AddDays(2));
    private static CreateCourseSessionRequest SessionRequest(Guid version) => new("Session 1", Start, Start.AddHours(1),
        "https://example.com/meeting", null, 0, null, version);

    [Fact]
    public async Task DraftToPending_PersistsSingleHierarchy_AndUserAudit()
    {
        await using var f = await Fixture.CreateAsync();
        var draft = await f.Service.CreateAsync(1, 1, DraftRequest);
        Assert.Equal("Draft", draft.Status);
        Assert.Empty(draft.Sessions);
        Assert.Equal("SESSION_REQUIRED", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.SubmitAsync(1, draft.Id, draft.Version))).Code);
        var scheduled = await f.Service.AddSessionAsync(1, draft.Id, SessionRequest(draft.Version));
        Assert.NotEqual(draft.Version, scheduled.Version);
        var pending = await f.Service.SubmitAsync(1, draft.Id, scheduled.Version);
        Assert.Equal("PendingApproval", pending.Status);
        Assert.NotNull(pending.SubmittedAt);
        Assert.Null(pending.ConfirmedByCreatorId);
        Assert.Equal(pending.Version, (await f.Service.SubmitAsync(1, draft.Id, pending.Version)).Version);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Service.GetOwnedAsync(1, draft.Id);
        Assert.Equal(DateTimeKind.Utc, saved.StartsAt.Kind);
        Assert.Equal(DateTimeKind.Utc, saved.Sessions[0].StartsAt.Kind);
        Assert.Equal(draft.Id, Assert.Single(saved.Sessions).CourseIntakeId);
        Assert.Equal(1, await f.Db.Courses.CountAsync());
        Assert.Equal(1, await f.Db.CourseIntakes.CountAsync());
        var logs = await f.Db.AuditLogs.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(3, logs.Count);
        Assert.All(logs, x => { Assert.Equal(1, x.UserId); Assert.Null(x.AdminAccountId); Assert.Equal(draft.Id.ToString(), x.EntityId); });
        Assert.Equal("CourseIntakeSubmitted", logs[^1].Action);
    }

    [Theory]
    [InlineData(0, "UNAUTHENTICATED")]
    [InlineData(3, "TRAINER_REQUIRED")]
    [InlineData(4, "TRAINER_REQUIRED")]
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
        Assert.Empty(await f.Db.CourseIntakes.ToListAsync());
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Ownership_FiltersListsAndEveryMutatingCommand()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        var other = await f.Service.CreateAsync(2, 1, DraftRequest);
        Assert.Equal(d.Id, Assert.Single(await f.Service.ListOwnedAsync(1)).Id);
        var commands = new Func<Task>[] {
            async () => await f.Service.GetOwnedAsync(2, d.Id),
            async () => await f.Service.UpdateAsync(2, d.Id, new(DraftRequest.RegistrationOpensAt, DraftRequest.RegistrationClosesAt, Start, Start.AddDays(2), d.Version)),
            async () => await f.Service.AddSessionAsync(2, d.Id, SessionRequest(d.Version)),
            async () => await f.Service.UpdateSessionAsync(2, d.Id, 1, new("Changed", Start, Start.AddHours(1), "https://example.com", null, 0, null, d.Version)),
            async () => await f.Service.DeleteSessionAsync(2, d.Id, 1, d.Version),
            async () => await f.Service.SubmitAsync(2, d.Id, d.Version),
        };
        foreach (var command in commands)
            Assert.Equal("INTAKE_NOT_FOUND", (await Assert.ThrowsAsync<CourseIntakeException>(command)).Code);
        Assert.Equal(2, other.TrainerId);
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync());
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
            0 => r with { StartsAt = Start.AddHours(-1) },
            1 => r with { EndsAt = Start },
            2 => r with { MeetingLink = "javascript:alert(1)" },
            3 => r with { PhysicalCapacity = -1 },
            4 => r with { PhysicalAddress = "Room 1", PhysicalCapacity = 10, PhysicalBookingDeadline = Start.AddMinutes(1) },
            _ => r with { MeetingLink = null },
        };
        await Assert.ThrowsAsync<CourseIntakeException>(() => f.Service.AddSessionAsync(1, d.Id, r));
        Assert.Equal(d.Version, (await f.Service.GetOwnedAsync(1, d.Id)).Version);
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
    [InlineData(CourseIntakeStatus.Published)]
    [InlineData(CourseIntakeStatus.InProgress)]
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
        Assert.Equal("PendingApproval", (await f.Service.SubmitAsync(1, d.Id, d.Version)).Status);
    }

    [Fact]
    public async Task ConcurrentSessionEdits_RollBackLosingSessionAndAudit()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        await using var secondDb = new CoLearnXDbContext(f.Options);
        var second = new CourseIntakeService(secondDb);
        await second.GetOwnedAsync(1, d.Id); // read the same original version before the winning edit
        await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        var error = await Assert.ThrowsAsync<CourseIntakeException>(() => second.AddSessionAsync(1, d.Id, SessionRequest(d.Version)));
        Assert.Equal("INTAKE_VERSION_CONFLICT", error.Code);
        Assert.Equal(1, await f.Db.CourseSessions.CountAsync());
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync());
        Assert.Equal("INTAKE_VERSION_CONFLICT", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.SubmitAsync(1, d.Id, d.Version))).Code);
    }

    [Fact]
    public async Task ReferencedSession_CannotBeDeleted_ByServiceOrDatabase()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        d = await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        var id = Assert.Single(d.Sessions).Id;
        f.Db.AttendanceRecords.Add(new AttendanceRecord { CourseSessionId = id, UserId = 3, RecordedByTrainerId = 1 });
        await f.Db.SaveChangesAsync();
        Assert.Equal("SESSION_HAS_HISTORY", (await Assert.ThrowsAsync<CourseIntakeException>(
            () => f.Service.DeleteSessionAsync(1, d.Id, id, d.Version))).Code);
        f.Db.ChangeTracker.Clear();
        f.Db.CourseSessions.Remove(await f.Db.CourseSessions.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task LegacyCatalogAndEnrolment_DoNotExposeDraftSessions()
    {
        await using var f = await Fixture.CreateAsync();
        var d = await f.Service.CreateAsync(1, 1, DraftRequest);
        d = await f.Service.AddSessionAsync(1, d.Id, SessionRequest(d.Version));
        var detail = await new CourseService(f.Db).GetByIdAsync(1, 3);
        Assert.Empty(detail!.Sessions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EnrollmentService(f.Db).EnrolAsync(3,
            new EnrolRequest(1, Assert.Single(d.Sessions).Id)));
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
        await f.Db.SaveChangesAsync();
        Assert.Single((await new CourseService(f.Db).GetByIdAsync(1, 3))!.Sessions);
        var service = new EnrollmentService(f.Db);
        var request = new EnrolRequest(1, Assert.Single(d.Sessions).Id);
        var result = await service.EnrolAsync(3, request);
        Assert.Equal(80, result.BalanceAfter);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnrolAsync(3, request));
        Assert.Equal(1, await f.Db.Enrollments.CountAsync());
        var ledger = await f.Db.CreditTransactions.SingleAsync();
        Assert.Equal(-20, ledger.Delta);
        Assert.Equal(80, ledger.BalanceAfter);
        Assert.Equal(1, (await f.Db.CourseSessions.SingleAsync()).SeatsTaken);
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
            () => f.Service.SubmitAsync(1, d.Id, d.Version))).Code);
        Assert.Equal("Draft", (await f.Service.GetOwnedAsync(1, d.Id)).Status);
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Database_RejectsOrphanSessionsAndInvalidIntakeDates()
    {
        await using var f = await Fixture.CreateAsync();
        f.Db.CourseSessions.Add(new CourseSession { CourseIntakeId = 999, StartsAt = Start, EndsAt = Start.AddHours(1) });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();
        f.Db.CourseIntakes.Add(new CourseIntake { CourseId = 1, TrainerId = 1, RegistrationOpensAt = Start,
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

    private sealed class Fixture(SqliteConnection connection, CoLearnXDbContext db, DbContextOptions<CoLearnXDbContext> options) : IAsyncDisposable
    {
        public CoLearnXDbContext Db { get; } = db;
        public DbContextOptions<CoLearnXDbContext> Options { get; } = options;
        public CourseIntakeService Service { get; } = new(db);
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options;
            var db = new CoLearnXDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(Enumerable.Range(1, 4).Select(id => new User { Id = id, Email = $"b1-{id}@example.com", FullName = $"User {id}",
                CreditBalance = 100, IsActive = id != 4,
                Roles = [new UserRole { Role = id == 3 ? AppRole.Member : AppRole.Trainer }] }));
            db.Courses.Add(new Course { Id = 1, Code = "B1", Title = "B1 Test", TrainerId = 1, CreatorId = 1, Status = CourseStatus.Published, CreditCost = 20 });
            await db.SaveChangesAsync();
            return new Fixture(connection, db, options);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
