using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BS2.Application.Sync;

/// <summary>
/// Background entry point: one fresh DI scope (and DbContext) per user that has an active
/// connection, so one user's failure never poisons another's unit of work.
/// </summary>
public sealed class SyncAllUsersService(IServiceScopeFactory scopeFactory, ILogger<SyncAllUsersService> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        List<Guid> userIds;
        using (var scope = scopeFactory.CreateScope())
        {
            userIds = await scope.ServiceProvider.GetRequiredService<IAppDbContext>().BankConnections
                .Where(c => c.Status == ConnectionStatus.Active)
                .Select(c => c.UserId).Distinct()
                .ToListAsync(ct);
        }

        foreach (var userId in userIds)
        {
            ct.ThrowIfCancellationRequested();
            using var scope = scopeFactory.CreateScope();
            try
            {
                var run = await scope.ServiceProvider.GetRequiredService<SyncService>().RunAsync(userId, SyncTrigger.Scheduled, ct);
                logger.LogInformation("Scheduled sync for user {UserId}: {Status}, {New} new transaction(s).", userId, run.Status, run.TransactionsNew);
            }
            catch (ConflictException)
            {
                logger.LogInformation("Scheduled sync for user {UserId} skipped: a run is already in progress.", userId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Scheduled sync for user {UserId} failed.", userId);
            }
        }
    }
}
