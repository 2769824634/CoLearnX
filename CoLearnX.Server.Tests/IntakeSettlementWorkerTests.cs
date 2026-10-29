using CoLearnX.Server.Data;
using CoLearnX.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoLearnX.Server.Tests;

public sealed class IntakeSettlementWorkerTests
{
    [Fact]
    public async Task Stopping_while_waiting_for_the_next_scan_completes_without_a_cancellation_fault()
    {
        var settlement = new SignallingSettlementService();
        await using var services = new ServiceCollection()
            .AddSingleton<IIntakeSettlementService>(settlement)
            .AddSingleton<IBusinessNotificationMailSender, DisabledMailSender>()
            .AddDbContext<CoLearnXDbContext>(options => options.UseSqlite("Data Source=:memory:"))
            .AddScoped<NotificationEmailDispatcher>()
            .BuildServiceProvider();
        var worker = new IntakeSettlementWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<IntakeSettlementWorker>.Instance);
        using var stopping = new CancellationTokenSource();

        await worker.StartAsync(stopping.Token);
        await settlement.FirstScan.WaitAsync(TimeSpan.FromSeconds(5));
        stopping.Cancel();

        var exception = await Record.ExceptionAsync(() => worker.ExecuteTask!);

        Assert.Null(exception);
    }

    private sealed class SignallingSettlementService : IIntakeSettlementService
    {
        private readonly TaskCompletionSource _firstScan = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstScan => _firstScan.Task;

        public Task ProcessDueAsync(CancellationToken ct = default)
        {
            _firstScan.TrySetResult();
            return Task.CompletedTask;
        }

        public Task<IntakeSettlementResult> SettleIfDueAsync(int intakeId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IntakeSettlementResult> CancelIntakeAsync(int creatorUserId, int intakeId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class DisabledMailSender : IBusinessNotificationMailSender
    {
        public bool IsAvailable => false;

        public Task SendAsync(string email, string title, string body, string targetPath, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
