using System.Collections.Concurrent;
using System.Data.Common;
using CoLearnX.Server.Contracts.Dtos;
using CoLearnX.Server.Data;
using CoLearnX.Server.Domain.Entities;
using CoLearnX.Server.Domain.Enums;
using CoLearnX.Server.Services;
using CoLearnX.Server.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CoLearnX.Server.Tests;

/// <summary>
/// SQLite does not exercise SQL Server's retrying provider. These tests replace
/// the execution-strategy factory with a deterministic strategy and inject a
/// post-commit ACK loss to verify the application-level retry contract.
/// </summary>
public sealed class FirstRoundRetryStrategyTests
{
    private static readonly DateTime IntakeStart = new(2027, 1, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OrdinaryIntakeCreate_RetriesAfterCommittedAckLoss_WithoutDuplicate()
    {
        await using var harness = await TestDatabase.CreateAsync();
        await SeedIntakeAsync(harness.Db);

        using (harness.Activate())
        {
            harness.Probe.LoseNextCommitAck();
            var result = await new CourseIntakeService(harness.Db).CreateAsync(
                1,
                1,
                new CreateCourseIntakeRequest(
                    IntakeStart.AddDays(-20),
                    IntakeStart.AddDays(-10),
                    IntakeStart,
                    IntakeStart.AddDays(2),
                    MinEnrollment: 2));

            Assert.Equal("Draft", result.Status);
            Assert.NotEqual(Guid.Empty, result.Version);
        }

        Assert.Equal(1, await harness.Db.CourseIntakes.CountAsync());
        Assert.Equal(1, await harness.Db.AuditLogs.CountAsync(item => item.Action == "CourseIntakeCreated"));
        Assert.Equal(1, harness.Probe.RetryCount);
        harness.Probe.AssertExplicitTransactionsRanInsideStrategy();
    }

    [Fact]
    public async Task MaterialVersionCreate_RetriesByStableFilePath_WithoutDuplicate()
    {
        await using var harness = await TestDatabase.CreateAsync();
        await SeedMaterialAsync(harness.Db);
        const string filePath = "materials/retry-stable.pdf";

        using (harness.Activate())
        {
            harness.Probe.LoseNextCommitAck();
            var result = await new MaterialVersionService(harness.Db, new ProbeFileStorage()).CreateAsync(
                2,
                new CreateMaterialVersionRequest(
                    "Retry stable material",
                    "Material used for the commit ACK retry test.",
                    filePath,
                    "PDF",
                    "Testing",
                    2));

            Assert.Equal("PendingApproval", result.Status);
            Assert.Equal(filePath, result.FilePath);
            Assert.True(result.FileSizeBytes > 0);
        }

        Assert.Equal(1, await harness.Db.LearningMaterials.CountAsync(item => item.FilePath == filePath));
        Assert.Equal(1, await harness.Db.CourseMaterialVersions.CountAsync(item => item.FilePath == filePath));
        Assert.Equal(1, await harness.Db.AuditLogs.CountAsync(item => item.Action == "MaterialVersionSubmitted"));
        Assert.Equal(1, harness.Probe.RetryCount);
        harness.Probe.AssertExplicitTransactionsRanInsideStrategy();
    }

    [Fact]
    public async Task DisputeCreate_RetriesAfterCommittedAckLoss_WithoutDuplicate()
    {
        await using var harness = await TestDatabase.CreateAsync();
        await SeedDisputeAsync(harness.Db);

        using (harness.Activate())
        {
            harness.Probe.LoseNextCommitAck();
            var result = await new AdminFinanceService(harness.Db).CreateDisputeAsync(
                3,
                new CreateDisputeRequest(3, "Retry dispute submission"));

            Assert.Equal("Open", result.Status);
            Assert.Equal(3, result.EnrollmentId);
            Assert.Equal(3, result.CourseIntakeId);
            Assert.Equal(3, result.CourseSessionId);
        }

        Assert.Equal(1, await harness.Db.Disputes.CountAsync(item => item.EnrollmentId == 3));
        Assert.Equal(1, await harness.Db.AuditLogs.CountAsync(item => item.Action == "DisputeSubmitted"));
        Assert.Equal(1, harness.Probe.RetryCount);
        harness.Probe.AssertExplicitTransactionsRanInsideStrategy();
    }

    private static async Task SeedIntakeAsync(CoLearnXDbContext db)
    {
        db.Users.Add(new User
        {
            Id = 1,
            Email = "retry-trainer@example.com",
            FullName = "Retry Trainer",
            Roles = [new UserRole { Role = AppRole.Trainer }],
        });
        db.Courses.Add(new Course
        {
            Id = 1,
            Code = "RETRY-INTAKE",
            Title = "Retry Intake",
            TrainerId = 1,
            CreatorId = 1,
            Status = CourseStatus.Published,
            CreditCost = 20,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedMaterialAsync(CoLearnXDbContext db)
    {
        db.Users.Add(new User
        {
            Id = 2,
            Email = "retry-creator@example.com",
            FullName = "Retry Creator",
            Roles = [new UserRole { Role = AppRole.Creator }],
        });
        db.Courses.Add(new Course
        {
            Id = 2,
            Code = "RETRY-MATERIAL",
            Title = "Retry Material",
            TrainerId = 2,
            CreatorId = 2,
            Status = CourseStatus.Published,
            CreditCost = 20,
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedDisputeAsync(CoLearnXDbContext db)
    {
        db.Users.AddRange(
            new User
            {
                Id = 1,
                Email = "retry-dispute-trainer@example.com",
                FullName = "Dispute Trainer",
                Roles = [new UserRole { Role = AppRole.Trainer }],
            },
            new User
            {
                Id = 3,
                Email = "retry-dispute-member@example.com",
                FullName = "Dispute Member",
                CreditBalance = 100,
                Roles = [new UserRole { Role = AppRole.Member }],
            });
        db.Courses.Add(new Course
        {
            Id = 3,
            Code = "RETRY-DISPUTE",
            Title = "Retry Dispute",
            TrainerId = 1,
            CreatorId = 1,
            Status = CourseStatus.Published,
            CreditCost = 25,
        });
        db.CourseIntakes.Add(new CourseIntake
        {
            Id = 3,
            CourseId = 3,
            TrainerId = 1,
            RegistrationOpensAt = IntakeStart.AddDays(-20),
            RegistrationClosesAt = IntakeStart.AddDays(-10),
            StartsAt = IntakeStart,
            EndsAt = IntakeStart.AddDays(2),
            Status = CourseIntakeStatus.Published,
            MinEnrollment = 2,
        });
        db.CourseSessions.Add(new CourseSession
        {
            Id = 3,
            CourseIntakeId = 3,
            Label = "Dispute session",
            StartsAt = IntakeStart,
            EndsAt = IntakeStart.AddHours(1),
        });
        db.Enrollments.Add(new Enrollment
        {
            Id = 3,
            UserId = 3,
            CourseId = 3,
            CourseSessionId = 3,
            Status = EnrollmentStatus.Active,
            CreditsSpent = 25,
        });
        await db.SaveChangesAsync();
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private TestDatabase(SqliteConnection connection, CoLearnXDbContext db, RetryProbe probe)
        {
            Connection = connection;
            Db = db;
            Probe = probe;
        }

        public SqliteConnection Connection { get; }
        public CoLearnXDbContext Db { get; }
        public RetryProbe Probe { get; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var probe = new RetryProbe();
            var options = new DbContextOptionsBuilder<CoLearnXDbContext>()
                .UseSqlite(connection)
                .ReplaceService<IExecutionStrategyFactory, ProbeExecutionStrategyFactory>()
                .AddInterceptors(new TransactionProbeInterceptor(probe))
                .Options;
            var db = new CoLearnXDbContext(options);
            using (RetryProbe.Activate(probe))
            {
                await db.Database.EnsureCreatedAsync();
            }
            return new TestDatabase(connection, db, probe);
        }

        public IDisposable Activate() => RetryProbe.Activate(Probe);

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class RetryProbe
    {
        private static readonly AsyncLocal<RetryProbe?> Current = new();
        private int _commitAckLosses;
        private int _retryCount;
        private int _commitAckFailuresInjected;
        private readonly ConcurrentQueue<bool> _transactionStartedInsideStrategy = new();
        private readonly ConcurrentQueue<bool> _transactionCommittedInsideStrategy = new();

        public int RetryCount => Volatile.Read(ref _retryCount);

        public static IDisposable Activate(RetryProbe probe)
        {
            var previous = Current.Value;
            Current.Value = probe;
            return new Scope(() => Current.Value = previous);
        }

        public void LoseNextCommitAck() => Interlocked.Exchange(ref _commitAckLosses, 1);

        public static RetryProbe? GetCurrent() => Current.Value;

        public void RecordTransactionStarted(bool insideStrategy)
            => _transactionStartedInsideStrategy.Enqueue(insideStrategy);

        public void RecordTransactionCommitted(bool insideStrategy)
            => _transactionCommittedInsideStrategy.Enqueue(insideStrategy);

        public bool TryLoseCommitAck()
        {
            if (Interlocked.CompareExchange(ref _commitAckLosses, 0, 1) != 1)
                return false;
            Interlocked.Increment(ref _commitAckFailuresInjected);
            return true;
        }

        public void RecordRetry() => Interlocked.Increment(ref _retryCount);

        public void AssertExplicitTransactionsRanInsideStrategy()
        {
            Assert.NotEmpty(_transactionStartedInsideStrategy);
            Assert.NotEmpty(_transactionCommittedInsideStrategy);
            Assert.All(_transactionStartedInsideStrategy, Assert.True);
            Assert.All(_transactionCommittedInsideStrategy, Assert.True);
            Assert.Equal(1, Volatile.Read(ref _commitAckFailuresInjected));
        }

        private sealed class Scope(Action release) : IDisposable
        {
            public void Dispose() => release();
        }
    }

    private sealed class ProbeExecutionStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    {
        public IExecutionStrategy Create()
            => new ProbeExecutionStrategy(dependencies, RetryProbe.GetCurrent() ?? new RetryProbe());
    }

    private sealed class ProbeExecutionStrategy(ExecutionStrategyDependencies dependencies, RetryProbe probe)
        : ExecutionStrategy(dependencies, maxRetryCount: 1, maxRetryDelay: TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception)
            => exception is SimulatedCommitAckLostException || exception.InnerException is SimulatedCommitAckLostException;

        protected override void OnRetry()
        {
            _ = probe;
            probe.RecordRetry();
            base.OnRetry();
        }
    }

    private sealed class TransactionProbeInterceptor(RetryProbe probe) : DbTransactionInterceptor
    {
        public override DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction transaction)
        {
            var result = base.TransactionStarted(connection, eventData, transaction);
            probe.RecordTransactionStarted(ExecutionStrategy.Current is ProbeExecutionStrategy);
            return result;
        }

        public override async ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData,
            DbTransaction transaction, CancellationToken cancellationToken = default)
        {
            var result = await base.TransactionStartedAsync(connection, eventData, transaction, cancellationToken);
            probe.RecordTransactionStarted(ExecutionStrategy.Current is ProbeExecutionStrategy);
            return result;
        }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            base.TransactionCommitted(transaction, eventData);
            probe.RecordTransactionCommitted(ExecutionStrategy.Current is ProbeExecutionStrategy);
            if (probe.TryLoseCommitAck())
                throw new SimulatedCommitAckLostException();
        }

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await base.TransactionCommittedAsync(transaction, eventData, cancellationToken);
            probe.RecordTransactionCommitted(ExecutionStrategy.Current is ProbeExecutionStrategy);
            if (probe.TryLoseCommitAck())
                throw new SimulatedCommitAckLostException();
        }
    }

    private sealed class SimulatedCommitAckLostException() : Exception("Simulated commit ACK loss after the database commit.");

    private sealed class ProbeFileStorage : IFileStorage
    {
        public string Provider => "Probe";
        public bool CanIssueCloudLinks => false;
        public string? Container => null;

        public Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<Stream?> OpenAsync(string key, CancellationToken ct = default)
            => Task.FromResult<Stream?>(null);

        public Task<Uri?> TryCreateReadUriAsync(string key, TimeSpan lifetime, CancellationToken ct = default)
            => Task.FromResult<Uri?>(null);

        public Task<StorageFileInfo?> GetInfoAsync(string key, CancellationToken ct = default)
            => Task.FromResult<StorageFileInfo?>(new StorageFileInfo(128));
    }
}
