using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BS2.Application.Banking;

public sealed class BankConnectionService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IBankingProvider provider,
    IClock clock,
    AuthorizationFlowService flow,
    ILogger<BankConnectionService> logger)
{
    public Task<IReadOnlyList<ProviderBank>> GetBanksAsync(string country, CancellationToken ct = default) =>
        provider.GetBanksAsync((country ?? "").Trim().ToUpperInvariant(), ct);

    public async Task<BankConnectionDto[]> ListAsync(CancellationToken ct = default)
    {
        var connections = await Query().OrderBy(c => c.BankName).ToListAsync(ct);
        return await MapAsync(connections, ct);
    }

    public async Task<BankConnectionDto> GetAsync(Guid id, CancellationToken ct = default) =>
        (await MapAsync([await FindAsync(id, ct)], ct))[0];

    public async Task<BankConnectionDto> CreateAsync(UpsertBankConnectionRequest request, CancellationToken ct = default)
    {
        var (bankName, country, ibans) = Validate(request);
        await EnsureUniqueAsync(bankName, null, ct);

        var now = clock.UtcNow;
        var connection = new BankConnection
        {
            Id = Guid.NewGuid(),
            UserId = currentUser.UserId,
            Provider = BankProvider.EnableBanking,
            BankName = bankName,
            Country = country,
            PsuType = string.IsNullOrWhiteSpace(request.PsuType) ? "personal" : request.PsuType.Trim(),
            SelectAccountsAtBank = request.SelectAccountsAtBank ?? false,
            ConsentValidityDays = request.ConsentValidityDays,
            ConfiguredIbans = ibans,
            Status = ConnectionStatus.NotAuthorized,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.BankConnections.Add(connection);
        await db.SaveChangesAsync(ct);
        return await GetAsync(connection.Id, ct);
    }

    public async Task<BankConnectionDto> UpdateAsync(Guid id, UpsertBankConnectionRequest request, CancellationToken ct = default)
    {
        var connection = await FindAsync(id, ct);
        var (bankName, country, ibans) = Validate(request);
        await EnsureUniqueAsync(bankName, id, ct);

        connection.BankName = bankName;
        connection.Country = country;
        if (!string.IsNullOrWhiteSpace(request.PsuType)) connection.PsuType = request.PsuType.Trim();
        if (request.SelectAccountsAtBank is { } select) connection.SelectAccountsAtBank = select;
        connection.ConsentValidityDays = request.ConsentValidityDays;
        connection.ConfiguredIbans = ibans;
        connection.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Deletes the provider session (best effort). Hard-deletes when no transactions reference it, else marks Revoked.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await FindAsync(id, ct);

        if (connection.ExternalSessionId is { } sessionId)
        {
            try { await provider.DeleteSessionAsync(sessionId, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "{Bank}: could not delete provider session.", connection.BankName); }
        }

        db.PendingAuthorizations.RemoveRange(await db.PendingAuthorizations.Where(p => p.BankConnectionId == id).ToListAsync(ct));

        var hasTransactions = await db.Transactions.AnyAsync(t => t.Account.BankConnectionId == id, ct);
        if (hasTransactions)
        {
            connection.Status = ConnectionStatus.Revoked;
            connection.ExternalSessionId = null;
            connection.ValidUntil = null;
            connection.UpdatedAt = clock.UtcNow;
        }
        else
        {
            db.Accounts.RemoveRange(connection.Accounts);
            db.BankConnections.Remove(connection);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<StartAuthorizationResult> StartAuthorizationAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await FindAsync(id, ct);
        var (pending, _) = await flow.EnsurePendingAsync(connection, ct);
        return new StartAuthorizationResult(pending.Url, pending.State, pending.ExpiresAt);
    }

    /// <summary>Completes only an authorization the current user started.</summary>
    public Task<AuthorizationResult> CompleteAuthorizationAsync(string? code, string? state, CancellationToken ct = default) =>
        flow.CompleteAsync(code, state, currentUser.UserId, ct);

    /// <summary>Re-reads the provider session and updates status, validity and accounts.</summary>
    public async Task<BankConnectionDto> RefreshAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await FindAsync(id, ct);
        if (connection.ExternalSessionId is null) return await GetAsync(id, ct);

        var session = await provider.GetSessionAsync(connection.ExternalSessionId, ct);
        var now = clock.UtcNow;
        if (session is null)
        {
            connection.Status = ConnectionStatus.Expired;
        }
        else
        {
            connection.ValidUntil = session.ValidUntil;
            connection.Status = session.ValidUntil is { } until && until < now ? ConnectionStatus.Expired : ConnectionStatus.Active;
            await flow.UpsertAccountsAsync(connection, session.Accounts, ct);
        }

        connection.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private IQueryable<BankConnection> Query() =>
        db.BankConnections.Include(c => c.Accounts).Where(c => c.UserId == currentUser.UserId);

    private async Task<BankConnection> FindAsync(Guid id, CancellationToken ct) =>
        await Query().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Bank connection not found.");

    private static (string BankName, string Country, IReadOnlyList<string> Ibans) Validate(UpsertBankConnectionRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var bankName = (request.BankName ?? "").Trim();
        var country = (request.Country ?? "").Trim().ToUpperInvariant();

        if (bankName.Length == 0) errors["bankName"] = ["Bank name is required."];
        if (country.Length != 2 || !country.All(char.IsAsciiLetterUpper)) errors["country"] = ["Country must be a two-letter code."];
        if (request.ConsentValidityDays is <= 0) errors["consentValidityDays"] = ["Consent validity must be positive."];

        var ibans = (request.Ibans ?? []).Select(Iban.Normalize).Where(i => i.Length > 0).Distinct().ToList();
        if (errors.Count > 0) throw new ValidationException(errors);
        return (bankName, country, ibans);
    }

    private async Task EnsureUniqueAsync(string bankName, Guid? exceptId, CancellationToken ct)
    {
        var exists = await db.BankConnections.AnyAsync(c =>
            c.UserId == currentUser.UserId && c.Provider == BankProvider.EnableBanking && c.BankName == bankName && c.Id != exceptId, ct);
        if (exists) throw new ConflictException($"A connection for '{bankName}' already exists.");
    }

    private async Task<BankConnectionDto[]> MapAsync(List<BankConnection> connections, CancellationToken ct)
    {
        var ids = connections.Select(c => c.Id).ToList();
        var now = clock.UtcNow;

        var pending = (await db.PendingAuthorizations
                .Where(p => ids.Contains(p.BankConnectionId) && p.ExpiresAt > now)
                .ToListAsync(ct))
            .GroupBy(p => p.BankConnectionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First());

        var counts = await db.Transactions
            .Where(t => t.UserId == currentUser.UserId)
            .GroupBy(t => t.AccountId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return connections.Select(c => new BankConnectionDto(
            c.Id, c.Provider, c.BankName, c.Country, c.PsuType, c.SelectAccountsAtBank, c.ConsentValidityDays,
            c.ConfiguredIbans, c.Status, c.ValidUntil, c.LastAuthorizedAt, c.LastSyncedAt, c.LastSyncError,
            c.Accounts.OrderBy(a => a.Identifier).Select(a => AccountService.ToDto(a, c.BankName, counts.GetValueOrDefault(a.Id))).ToList(),
            pending.TryGetValue(c.Id, out var p) ? new PendingAuthorizationDto(p.Url, p.CreatedAt, p.ExpiresAt) : null)).ToArray();
    }
}
