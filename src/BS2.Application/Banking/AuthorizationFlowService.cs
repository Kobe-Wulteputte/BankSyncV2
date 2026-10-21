using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BS2.Application.Banking;

/// <summary>
/// Start/complete of a provider authorization. Deliberately has no <see cref="ICurrentUser"/>:
/// the callback is anonymous and the expiry notifier runs for every user, so the connection row
/// is the only source of the user id.
/// </summary>
public sealed class AuthorizationFlowService(
    IAppDbContext db,
    IBankingProvider provider,
    IFieldEncryptor encryptor,
    IClock clock,
    IOptions<SyncOptions> syncOptions,
    IOptions<AppOptions> appOptions,
    ILogger<AuthorizationFlowService> logger)
{
    /// <summary>
    /// Returns the outstanding non-expired link for the connection, or starts a new one (replacing
    /// any stale rows). <c>Created</c> tells the caller whether a mail is due.
    /// </summary>
    public async Task<(PendingAuthorization Pending, bool Created)> EnsurePendingAsync(BankConnection connection, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var existing = await db.PendingAuthorizations
            .Where(p => p.BankConnectionId == connection.Id)
            .ToListAsync(ct);

        var live = existing.Where(p => p.ExpiresAt > now).OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        if (live is not null) return (live, false);

        var state = Guid.NewGuid().ToString("N");
        var redirectUrl = new Uri($"{appOptions.Value.PublicOrigin.TrimEnd('/')}/api/banks/callback");
        var validityDays = connection.ConsentValidityDays ?? syncOptions.Value.ConsentValidityDays;

        var url = await provider.StartAuthorizationAsync(connection, state, redirectUrl, validityDays, ct);

        db.PendingAuthorizations.RemoveRange(existing);
        var pending = new PendingAuthorization
        {
            Id = Guid.NewGuid(),
            UserId = connection.UserId,
            BankConnectionId = connection.Id,
            State = state,
            Url = url,
            CreatedAt = now,
            ExpiresAt = now.AddHours(syncOptions.Value.AuthorizationLinkTtlHours)
        };
        db.PendingAuthorizations.Add(pending);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("{Bank}: authorization started for connection {ConnectionId}.", connection.BankName, connection.Id);
        return (pending, true);
    }

    /// <summary>
    /// Verifies <paramref name="state"/> against stored pending authorizations, exchanges the code,
    /// stores the session, re-points accounts and retires the superseded provider session.
    /// Only <paramref name="userId"/>'s own authorizations complete, so a forwarded link cannot
    /// attach someone else's bank to the connection.
    /// </summary>
    public async Task<AuthorizationResult> CompleteAsync(string? code, string? state, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return new AuthorizationResult(false, "", "No authorization code was supplied.", null);
        if (string.IsNullOrWhiteSpace(state)) return new AuthorizationResult(false, "", "No state was supplied.", null);

        var pending = await db.PendingAuthorizations
            .Include(p => p.BankConnection).ThenInclude(c => c.Accounts)
            .SingleOrDefaultAsync(p => p.State == state, ct);

        if (pending is not null && pending.BankConnection.UserId != userId)
        {
            // Left in place: the owner can still use it. Same answer as unknown, so state ids leak nothing.
            logger.LogWarning("Rejected callback for connection {ConnectionId}: started by another user.", pending.BankConnectionId);
            pending = null;
        }

        if (pending is null)
        {
            // Rejecting rather than trusting the code keeps a stray request from creating a session.
            logger.LogWarning("Rejected callback with unknown state {State}.", state);
            return new AuthorizationResult(false, "", "This authorization link is unknown or has already been used.", null);
        }

        var connection = pending.BankConnection;
        if (pending.ExpiresAt <= clock.UtcNow)
        {
            db.PendingAuthorizations.Remove(pending);
            await db.SaveChangesAsync(ct);
            return new AuthorizationResult(false, connection.BankName, "This authorization link has expired; start a new one from the Banks page.", connection.UserId);
        }

        ProviderSession session;
        try
        {
            session = await provider.CompleteAuthorizationAsync(code.Trim(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "{Bank}: authorization failed.", connection.BankName);
            return new AuthorizationResult(false, connection.BankName, $"{connection.BankName}: authorization failed; see the server log.", connection.UserId);
        }

        var now = clock.UtcNow;
        var superseded = connection.ExternalSessionId;
        connection.ExternalSessionId = session.SessionId;
        connection.ValidUntil = session.ValidUntil;
        connection.Status = ConnectionStatus.Active;
        connection.LastAuthorizedAt = now;
        connection.LastSyncError = null;
        connection.UpdatedAt = now;

        var accounts = await UpsertAccountsAsync(connection, session.Accounts, ct);

        // Provider ids of the old session are dead; keep the accounts (history) but stop fetching them.
        foreach (var dead in connection.Accounts.Where(a => !accounts.Contains(a.Identifier)))
            dead.ExternalAccountId = null;

        var allPending = await db.PendingAuthorizations.Where(p => p.BankConnectionId == connection.Id).ToListAsync(ct);
        db.PendingAuthorizations.RemoveRange(allPending);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("{Bank}: session created covering {Count} account(s).", connection.BankName, accounts.Count);

        if (superseded is not null && superseded != session.SessionId)
        {
            try { await provider.DeleteSessionAsync(superseded, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "{Bank}: could not delete superseded session.", connection.BankName); }
        }

        if (accounts.Count == 0)
        {
            // Seen when the ASPSP is not linked to the Enable Banking profile: authorizes cleanly, exposes nothing.
            return new AuthorizationResult(true, connection.BankName,
                $"{connection.BankName} authorized, but the session exposes no accounts. Check that {connection.BankName} is linked to your Enable Banking profile.",
                connection.UserId);
        }

        var uncovered = connection.ConfiguredIbans.Where(i => !accounts.Contains(i)).ToList();
        var message = $"{connection.BankName} authorized, covering {accounts.Count} account(s).";
        if (uncovered.Count > 0) message += $" Not covered: {string.Join(", ", uncovered)}. Check the configured IBANs.";

        return new AuthorizationResult(true, connection.BankName, message, connection.UserId);
    }

    /// <summary>
    /// Upserts accounts by identifier hash, re-pointing rows that previously belonged to another
    /// connection (re-authorization, or a bank re-created). Returns the identifiers seen.
    /// Does not save.
    /// </summary>
    public async Task<HashSet<string>> UpsertAccountsAsync(BankConnection connection, IReadOnlyList<ProviderAccount> providerAccounts, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var byHash = (await db.Accounts.Where(a => a.UserId == connection.UserId).ToListAsync(ct))
            .ToDictionary(a => a.IdentifierHash);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pa in providerAccounts)
        {
            var identifier = pa.IsIban ? Iban.Normalize(pa.Identifier) : pa.Identifier.Trim();
            if (identifier.Length == 0 || !seen.Add(identifier)) continue;

            var hash = encryptor.Hash(identifier);
            if (!byHash.TryGetValue(hash, out var account))
            {
                account = new Account
                {
                    Id = Guid.NewGuid(),
                    UserId = connection.UserId,
                    Identifier = identifier,
                    IdentifierHash = hash,
                    CreatedAt = now
                };
                db.Accounts.Add(account);
                byHash[hash] = account;
            }

            account.BankConnectionId = connection.Id;
            account.BankConnection = connection;
            account.ExternalAccountId = pa.AccountId;
            if (!string.IsNullOrWhiteSpace(pa.Currency)) account.Currency = pa.Currency;
            // The user's own display name wins over the bank's label.
            if (string.IsNullOrWhiteSpace(account.DisplayName)) account.DisplayName = pa.Name;
            account.UpdatedAt = now;
        }

        return seen;
    }
}
