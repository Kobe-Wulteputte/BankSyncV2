using BS2.Application;
using BS2.Application.Sync;
using Microsoft.Extensions.Options;

namespace BS2.Api.Workers;

/// <summary>
/// Runs the scheduled sync and the expiry notifier for every user on a fixed interval.
/// Replaces V1's Windows scheduled task. <c>Sync:Interval = 0</c> disables it.
/// </summary>
public sealed class SyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> options,
    SyncSchedule schedule,
    ILogger<SyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.Interval;
        if (interval <= TimeSpan.Zero)
        {
            logger.LogInformation("Scheduled sync disabled (Sync:Interval is zero).");
            return;
        }

        schedule.NextRunUtc = DateTime.UtcNow + options.Value.InitialDelay;
        try
        {
            await Task.Delay(options.Value.InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(interval);
        do
        {
            schedule.NextRunUtc = DateTime.UtcNow + interval;
            await RunOnceAsync(stoppingToken);
        } while (await WaitAsync(timer, stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        // Notifications first: a bank whose consent is about to lapse should get its mail even when
        // the sync itself then fails on it. Each step has its own scope and catch so one cannot
        // take the other down.
        await StepAsync("Expiry notification", sp => sp.GetRequiredService<ExpiryNotificationService>().RunForAllUsersAsync(ct), ct);
        await StepAsync("Scheduled sync", sp => sp.GetRequiredService<SyncAllUsersService>().RunAsync(ct), ct);
    }

    private async Task StepAsync(string name, Func<IServiceProvider, Task> step, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await step(scope.ServiceProvider);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            logger.LogError(e, "{Step} failed.", name);
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

public sealed class SyncSchedule : ISyncSchedule
{
    public DateTime? NextRunUtc { get; set; }
}
