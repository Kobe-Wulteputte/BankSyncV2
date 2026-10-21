using System.Text.Json;
using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BS2.Application.Sync;

/// <summary>
/// Pulls transactions for one user's active Enable Banking connections. Takes the user id
/// explicitly so the API (current user) and the worker (every user) share one code path.
/// </summary>
public sealed class SyncService(
    IAppDbContext db,
    IBankingProvider provider,
    ITransactionClassificationService classifier,
    IClock clock,
    ISyncSchedule schedule,
    IOptions<SyncOptions> syncOptions,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<SyncService> logger)
{
    /// <summary>A Running row older than this is a crash leftover, not a live run.</summary>
    private static readonly TimeSpan StaleRunAfter = TimeSpan.FromMinutes(30);

    public async Task<SyncRunDto> RunAsync(Guid userId, SyncTrigger trigger, CancellationToken ct)
    {
        var now = clock.UtcNow;
        if (await db.SyncRuns.AnyAsync(r => r.UserId == userId && r.Status == SyncStatus.Running && r.StartedAt > now - StaleRunAfter, ct))
            throw new ConflictException("A sync is already running.");

        var run = new SyncRun { Id = Guid.NewGuid(), UserId = userId, Trigger = trigger, Status = SyncStatus.Running, StartedAt = now };
        db.SyncRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Partial unique index on running rows: another run won the race.
            throw new ConflictException("A sync is already running.");
        }

        var details = new List<SyncDetailDto>();
        try
        {
            var connections = await db.BankConnections.Include(c => c.Accounts)
                .Where(c => c.UserId == userId && c.Provider == BankProvider.EnableBanking && c.Status == ConnectionStatus.Active)
                .OrderBy(c => c.BankName)
                .ToListAsync(ct);

            foreach (var connection in connections)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    details.Add(await SyncConnectionAsync(connection, run, ct));
                }
                catch (Exception ex) when (IsRecoverable(ex, ct))
                {
                    logger.LogError(ex, "{Bank}: sync failed.", connection.BankName);
                    // Provider messages help the user fix a connection; anything else may be database internals.
                    var message = ex is BankingProviderException ? Truncate(ex.Message, 2000) : "Unexpected error; see the server log.";
                    connection.LastSyncError = message;
                    connection.UpdatedAt = clock.UtcNow;
                    details.Add(new SyncDetailDto(connection.BankName, "Failed", message, 0, 0));
                }
                await db.SaveChangesAsync(ct);
            }

            var failed = details.Count(d => d.Status == "Failed");
            run.Status = failed == 0 && run.ConnectionsSkipped == 0 ? SyncStatus.Succeeded
                : run.ConnectionsSynced == 0 && failed > 0 ? SyncStatus.Failed
                : SyncStatus.PartiallySucceeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Sync run {RunId} failed.", run.Id);
            run.Status = SyncStatus.Failed;
            run.Error = "Unexpected error; see the server log.";
            ResetTracker(run);
        }

        run.FinishedAt = clock.UtcNow;
        run.DetailsJson = JsonSerializer.Serialize(details, SyncRunDto.Json);
        await db.SaveChangesAsync(ct);
        return SyncRunDto.From(run);
    }

    /// <summary>Cancellation by the caller and failed saves (the tracker is poisoned) end the run instead.</summary>
    private static bool IsRecoverable(Exception ex, CancellationToken ct) =>
        ex is not DbUpdateException && !(ex is OperationCanceledException && ct.IsCancellationRequested);

    /// <summary>After a failed save the rejected entities stay tracked and would fail the next save too; keep only the run.</summary>
    private void ResetTracker(SyncRun run)
    {
        if (db is not DbContext context) return; // test fakes
        context.ChangeTracker.Clear();
        context.Update(run);
    }

    public async Task<SyncRunDto[]> GetRunsAsync(Guid userId, int take = 10, CancellationToken ct = default)
    {
        var runs = await db.SyncRuns.Where(r => r.UserId == userId)
            .OrderByDescending(r => r.StartedAt).Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
        return runs.Select(SyncRunDto.From).ToArray();
    }

    public async Task<SyncStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var last = await db.SyncRuns.Where(r => r.UserId == userId).OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(ct);
        var running = last is { Status: SyncStatus.Running } && last.StartedAt > clock.UtcNow - StaleRunAfter;
        return new SyncStatusDto(running, last is null ? null : SyncRunDto.From(last), schedule.NextRunUtc);
    }

    private async Task<SyncDetailDto> SyncConnectionAsync(BankConnection connection, SyncRun run, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var bank = connection.BankName;

        if (connection.ExternalSessionId is null)
            return Skip(run, bank, "No provider session. Authorize the bank first.");

        // Cheap path like V1: trust the stored validity; only ask the provider when we never learned it.
        if (connection.ValidUntil is null)
        {
            var session = await provider.GetSessionAsync(connection.ExternalSessionId, ct);
            if (session is null)
            {
                connection.Status = ConnectionStatus.Expired;
                return Skip(run, bank, "Provider session no longer exists. Re-authorize the bank.");
            }
            connection.ValidUntil = session.ValidUntil;
        }

        if (connection.ValidUntil is { } until && until < now)
        {
            connection.Status = ConnectionStatus.Expired;
            connection.UpdatedAt = now;
            return Skip(run, bank, $"Session expired on {until:yyyy-MM-dd}. Re-authorize the bank.");
        }

        var warnings = new List<string>();
        var accounts = SelectAccounts(connection, warnings);
        if (accounts.Count == 0)
            return Skip(run, bank, $"The session exposes no accounts to sync. Check that {bank} is linked to your Enable Banking profile.");

        var from = clock.Today.AddDays(-syncOptions.Value.RetrievalDays);
        int fetched = 0, added = 0, failedAccounts = 0;
        var providerRejected = false;

        foreach (var account in accounts)
        {
            IReadOnlyList<ProviderTransaction> rows;
            try
            {
                rows = await provider.GetTransactionsAsync(account.ExternalAccountId!, from, ct);
            }
            catch (Exception ex) when (IsRecoverable(ex, ct))
            {
                logger.LogError(ex, "{Bank}/{Account}: could not fetch transactions.", bank, Iban.Mask(account.Identifier));
                warnings.Add($"{account.Identifier}: {ex.Message}");
                failedAccounts++;
                providerRejected |= ex is BankingProviderException;
                continue;
            }

            fetched += rows.Count;
            var fresh = await AddNewAsync(connection, account, rows, ct);
            added += fresh.Count;
            await db.SaveChangesAsync(ct);

            if (classificationOptions.Value.ClassifyOnSync)
                run.TransactionsClassified += await ClassifyAsync(fresh, ct);
        }

        // The bank can end a session before the stored ValidUntil; only the provider's session status
        // shows it. Marking it Expired lets ExpiryNotificationService mail a re-authorization link.
        if (providerRejected && await provider.GetSessionAsync(connection.ExternalSessionId, ct) is null)
        {
            connection.Status = ConnectionStatus.Expired;
            warnings.Add("Provider session is no longer authorized. Re-authorize the bank.");
        }

        connection.LastSyncedAt = now;
        connection.LastSyncError = warnings.Count == 0 ? null : Truncate(string.Join(" | ", warnings), 2000);
        connection.UpdatedAt = now;

        run.TransactionsFetched += fetched;
        run.TransactionsNew += added;

        if (failedAccounts == accounts.Count)
        {
            return new SyncDetailDto(bank, "Failed", connection.LastSyncError, fetched, added);
        }

        run.ConnectionsSynced++;
        return new SyncDetailDto(bank, "Synced", connection.LastSyncError, fetched, added);
    }

    private static SyncDetailDto Skip(SyncRun run, string bank, string message)
    {
        run.ConnectionsSkipped++;
        return new SyncDetailDto(bank, "Skipped", message, 0, 0);
    }

    /// <summary>No configured IBANs: every active account. Otherwise only configured IBANs the session covers.</summary>
    private List<Account> SelectAccounts(BankConnection connection, List<string> warnings)
    {
        var usable = connection.Accounts.Where(a => a.IsActive && !string.IsNullOrEmpty(a.ExternalAccountId)).ToList();
        if (connection.SyncsAllAccounts) return usable;

        var selected = new List<Account>();
        foreach (var iban in connection.ConfiguredIbans)
        {
            var account = usable.FirstOrDefault(a => string.Equals(a.Identifier, iban, StringComparison.OrdinalIgnoreCase));
            if (account is null)
            {
                logger.LogWarning("{Bank}: session does not cover {Iban}.", connection.BankName, Iban.Mask(iban));
                warnings.Add($"Session does not cover {iban}; re-authorize to include it.");
                continue;
            }
            selected.Add(account);
        }
        return selected;
    }

    private async Task<List<Transaction>> AddNewAsync(BankConnection connection, Account account, IReadOnlyList<ProviderTransaction> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.ExternalId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        // Across all of this user's accounts at the same bank, not just this one: the Excel import keeps
        // the same entries on a "(imported)" account, and a re-authorization can create a new account.
        var bankName = connection.BankName.ToLower();
        var known = (await db.Transactions
                .Where(t => t.UserId == connection.UserId && t.Account.BankConnection.BankName.ToLower() == bankName && ids.Contains(t.ExternalId))
                .Select(t => t.ExternalId)
                .ToListAsync(ct))
            .ToHashSet();

        var now = clock.UtcNow;
        var today = clock.Today;
        var fresh = new List<Transaction>();

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.ExternalId))
            {
                logger.LogWarning("{Bank}/{Account}: transaction without external id skipped.", connection.BankName, Iban.Mask(account.Identifier));
                continue;
            }
            if (!known.Add(row.ExternalId)) continue;

            var tx = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = connection.UserId,
                AccountId = account.Id,
                Account = account,
                Source = TransactionSource.EnableBanking,
                ExternalId = row.ExternalId,
                Amount = row.Amount,
                Currency = string.IsNullOrWhiteSpace(row.Currency) ? account.Currency : row.Currency,
                Date = TransactionRules.EffectiveDate(row.ValueDate, row.BookingDate, today),
                BookingDate = row.BookingDate,
                ValueDate = row.ValueDate,
                CounterpartyName = row.CounterpartyName,
                CounterpartyIban = row.CounterpartyIban,
                Description = row.Description,
                RawJson = row.RawJson,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Transactions.Add(tx);
            fresh.Add(tx);
        }

        logger.LogInformation("{Bank}/{Account}: {Fetched} fetched, {New} new.", connection.BankName, Iban.Mask(account.Identifier), rows.Count, fresh.Count);
        return fresh;
    }

    private async Task<int> ClassifyAsync(List<Transaction> transactions, CancellationToken ct)
    {
        var classified = 0;
        foreach (var tx in transactions)
        {
            try
            {
                var result = await classifier.ClassifyAsync(tx, ClassificationTrigger.Sync, force: false, ct);
                if (result.Accepted) classified++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Classification failed for transaction {TransactionId}.", tx.Id);
            }
        }
        if (transactions.Count > 0) await db.SaveChangesAsync(ct);
        return classified;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
