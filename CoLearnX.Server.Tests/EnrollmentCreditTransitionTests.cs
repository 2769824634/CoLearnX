using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoLearnX.Server.Tests;

public class EnrollmentCreditTransitionTests
{
    [Fact]
    public async Task Hold_moves_available_credits_and_takes_a_seat()
    {
        await using var f = await Fixture.CreateAsync();
        var wallet = await f.Credits.HoldCreditsAndSeatAsync(1, 1, 20);
        await f.Db.SaveChangesAsync();

        Assert.Equal(80, wallet.BalanceAfter);
        Assert.Equal(20, wallet.HeldAfter);
        var user = await f.Db.Users.AsNoTracking().SingleAsync(u => u.Id == 1);
        Assert.Equal(80, user.CreditBalance);
        Assert.Equal(20, user.HeldCredits);
        Assert.Equal(1, (await f.Db.CourseSessions.AsNoTracking().SingleAsync()).SeatsTaken);
    }

    [Fact]
    public async Task Hold_rejects_insufficient_credits_and_a_full_session()
    {
        await using var f = await Fixture.CreateAsync();
        Assert.Equal("INSUFFICIENT_CREDITS", (await Assert.ThrowsAsync<CourseException>(
            () => f.Credits.HoldCreditsAndSeatAsync(1, 1, 101))).Code);

        var session = await f.Db.CourseSessions.SingleAsync();
        session.PhysicalCapacity = 1;
        session.SeatsTaken = 1;
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        Assert.Equal("SESSION_FULL", (await Assert.ThrowsAsync<CourseException>(
            () => f.Credits.HoldCreditsAndSeatAsync(1, 1, 20))).Code);
        Assert.Equal(100, (await f.Db.Users.AsNoTracking().SingleAsync(u => u.Id == 1)).CreditBalance);
    }

    [Fact]
    public async Task RecordHold_writes_the_hold_ledger_against_the_enrollment()
    {
        await using var f = await Fixture.CreateAsync();
        var wallet = await f.Credits.HoldCreditsAndSeatAsync(1, 1, 20);
        var enrollment = await f.AddReservedAsync(20);
        f.Credits.RecordHold(1, enrollment.Id, 20, wallet.BalanceAfter, wallet.HeldAfter, "Reserved B1");
        await f.Db.SaveChangesAsync();

        var row = Assert.Single(await f.Db.CreditTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(CreditTransactionType.Hold, row.Type);
        Assert.Equal(-20, row.Delta);
        Assert.Equal(80, row.BalanceAfter);
        Assert.Equal(20, row.HeldAfter);
        Assert.Equal(enrollment.Id, row.RelatedEnrollmentId);
        Assert.Equal(EnrollmentStatus.Reserved, enrollment.Status);
    }

    [Fact]
    public async Task Release_returns_held_credits_cancels_and_frees_the_seat()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Credits.HoldCreditsAndSeatAsync(1, 1, 20);
        var enrollment = await f.AddReservedAsync(20);
        f.Db.ChangeTracker.Clear();
        var loaded = await f.LoadEnrollmentAsync(enrollment.Id);
        f.Credits.Release(loaded.User, loaded, "Released B1");
        await f.Db.SaveChangesAsync();

        Assert.Equal(EnrollmentStatus.Cancelled, loaded.Status);
        Assert.Equal(0, loaded.CourseSession.SeatsTaken);
        Assert.Equal(100, loaded.User.CreditBalance);
        Assert.Equal(0, loaded.User.HeldCredits);
        var row = Assert.Single(await f.Db.CreditTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(CreditTransactionType.Release, row.Type);
        Assert.Equal(20, row.Delta);
    }

    [Fact]
    public async Task Capture_confirms_the_hold_without_changing_seats()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Credits.HoldCreditsAndSeatAsync(1, 1, 20);
        var enrollment = await f.AddReservedAsync(20);
        f.Db.ChangeTracker.Clear();
        var loaded = await f.LoadEnrollmentAsync(enrollment.Id);
        f.Credits.Capture(loaded.User, loaded, "Class confirmed: B1");
        await f.Db.SaveChangesAsync();

        Assert.Equal(EnrollmentStatus.Active, loaded.Status);
        Assert.Equal(1, loaded.CourseSession.SeatsTaken);
        Assert.Equal(80, loaded.User.CreditBalance);
        Assert.Equal(0, loaded.User.HeldCredits);
        var row = Assert.Single(await f.Db.CreditTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(CreditTransactionType.Capture, row.Type);
        Assert.Equal(0, row.Delta);
    }

    [Fact]
    public async Task Refund_restores_credits_marks_refunded_and_always_frees_the_seat()
    {
        await using var f = await Fixture.CreateAsync();
        var session = await f.Db.CourseSessions.SingleAsync();
        session.PhysicalCapacity = 0;
        session.SeatsTaken = 1;
        var enrollment = await f.AddActiveAsync(20);
        f.Db.ChangeTracker.Clear();
        var loaded = await f.LoadEnrollmentAsync(enrollment.Id);
        f.Credits.Refund(loaded.User, loaded, 14, "Withdrawal refund: B1", "6 credits retained after withdrawal from B1");
        await f.Db.SaveChangesAsync();

        Assert.Equal(EnrollmentStatus.Refunded, loaded.Status);
        Assert.Equal(0, loaded.CourseSession.SeatsTaken);
        Assert.Equal(114, loaded.User.CreditBalance);
        var ledger = await f.Db.CreditTransactions.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
        Assert.Equal(CreditTransactionType.Refund, ledger[0].Type);
        Assert.Equal(14, ledger[0].Delta);
        Assert.Equal(CreditTransactionType.Forfeit, ledger[1].Type);
        Assert.Equal(0, ledger[1].Delta);
    }

    [Fact]
    public async Task Refund_rejects_a_missing_seat_even_for_online_sessions()
    {
        await using var f = await Fixture.CreateAsync();
        var session = await f.Db.CourseSessions.SingleAsync();
        session.PhysicalCapacity = 0;
        session.SeatsTaken = 0;
        var enrollment = await f.AddActiveAsync(20);
        f.Db.ChangeTracker.Clear();
        var loaded = await f.LoadEnrollmentAsync(enrollment.Id);
        Assert.Equal("ENROLLMENT_SEAT_CONFLICT", Assert.Throws<CourseException>(
            () => f.Credits.Refund(loaded.User, loaded, 20, "Refund")).Code);
    }

    private sealed class Fixture(SqliteConnection connection, CoLearnXDbContext db) : IAsyncDisposable
    {
        public CoLearnXDbContext Db { get; } = db;
        public EnrollmentCreditTransitions Credits { get; } = new(db);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new CoLearnXDbContext(new DbContextOptionsBuilder<CoLearnXDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new User { Id = 1, Email = "member@example.com", FullName = "Member", CreditBalance = 100 });
            db.Courses.Add(new Course { Id = 1, Code = "B1", Title = "B1 Test", TrainerId = 1, CreatorId = 1, Status = CourseStatus.Published, CreditCost = 20 });
            db.CourseIntakes.Add(new CourseIntake
            {
                Id = 1, CourseId = 1, TrainerId = 1,
                RegistrationOpensAt = DateTime.UtcNow.AddDays(-20),
                RegistrationClosesAt = DateTime.UtcNow.AddDays(10),
                StartsAt = DateTime.UtcNow.AddDays(20),
                EndsAt = DateTime.UtcNow.AddDays(22),
                Status = CourseIntakeStatus.Published,
            });
            db.CourseSessions.Add(new CourseSession
            {
                Id = 1, CourseIntakeId = 1, Label = "Session 1",
                StartsAt = DateTime.UtcNow.AddDays(20), EndsAt = DateTime.UtcNow.AddDays(20).AddHours(1),
                PhysicalCapacity = 10,
            });
            await db.SaveChangesAsync();
            return new Fixture(connection, db);
        }

        public async Task<Enrollment> AddReservedAsync(int cost)
        {
            var enrollment = new Enrollment
            {
                UserId = 1, CourseId = 1, CourseSessionId = 1,
                Status = EnrollmentStatus.Reserved, CreditsSpent = cost,
            };
            Db.Enrollments.Add(enrollment);
            await Db.SaveChangesAsync();
            return enrollment;
        }

        public async Task<Enrollment> AddActiveAsync(int cost)
        {
            var enrollment = new Enrollment
            {
                UserId = 1, CourseId = 1, CourseSessionId = 1,
                Status = EnrollmentStatus.Active, CreditsSpent = cost,
            };
            Db.Enrollments.Add(enrollment);
            await Db.SaveChangesAsync();
            return enrollment;
        }

        public Task<Enrollment> LoadEnrollmentAsync(int id)
            => Db.Enrollments.Include(e => e.User).Include(e => e.CourseSession).SingleAsync(e => e.Id == id);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
