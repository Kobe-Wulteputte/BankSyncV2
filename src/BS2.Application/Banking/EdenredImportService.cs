using System.Text.Json;
using System.Text.Json.Serialization;
using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BS2.Application.Banking;

public sealed record EdenredImportResult(int Fetched, int New, int Classified);

/// <summary>
/// Imports a myEdenred <c>accounts/accountRef/operations</c> response, as sent by the bookmarklet.
/// Edenred has no API we can log in to from the server, so the user's own browser fetches the
/// operations and this only stores them. Everything lands on one "Edenred" account.
/// </summary>
public sealed class EdenredImportService(
    IAppDbContext db,
    ICurrentUser currentUser,
    ITransactionClassificationService classifier,
    IFieldEncryptor encryptor,
    IClock clock,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<EdenredImportService> logger)
{
    public const string Name = "Edenred";

    public async Task<EdenredImportResult> ImportAsync(JsonElement body, CancellationToken ct = default)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new ValidationException("data", "Expected the myEdenred operations response: { \"data\": [...] }.");

        var rows = new List<(EdenredOperation Op, string Raw)>();
        foreach (var element in data.EnumerateArray())
        {
            var op = element.Deserialize<EdenredOperation>();
            // Declined or pending payments never moved money.
            if (op is null || string.IsNullOrWhiteSpace(op.OperationRef) || op.Status is { } s && s != "success") continue;
            rows.Add((op, element.GetRawText()));
        }

        var userId = currentUser.UserId;
        var now = clock.UtcNow;
        var account = await EnsureAccountAsync(userId, now, ct);

        // Per user, like the Excel import: V1's workbook already holds these as EDENRED-<ref>.
        var ids = rows.Select(r => ExternalId(r.Op)).Distinct().ToList();
        var known = (await db.Transactions.Where(t => t.UserId == userId && ids.Contains(t.ExternalId)).Select(t => t.ExternalId).ToListAsync(ct))
            .ToHashSet();

        var fresh = new List<Transaction>();
        foreach (var (op, raw) in rows)
        {
            var externalId = ExternalId(op);
            if (!known.Add(externalId)) continue;

            var date = DateOnly.FromDateTime(op.Date);
            var tx = new Transaction
            {
                Id = Guid.NewGuid(), UserId = userId, AccountId = account.Id, Account = account, Source = TransactionSource.Edenred,
                ExternalId = externalId, Amount = (op.TransactionDetails?.Amount ?? 0) / 100m, Currency = op.Currency ?? "EUR",
                Date = date, BookingDate = date, CounterpartyName = op.Outlet?.OutletName ?? "", Description = Describe(op),
                RawJson = raw, CreatedAt = now, UpdatedAt = now
            };
            db.Transactions.Add(tx);
            fresh.Add(tx);
        }

        account.BankConnection.LastSyncedAt = now;
        account.BankConnection.LastSyncError = null;
        account.BankConnection.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var classified = 0;
        if (classificationOptions.Value.ClassifyOnSync)
        {
            foreach (var tx in fresh)
            {
                try
                {
                    if ((await classifier.ClassifyAsync(tx, ClassificationTrigger.Sync, force: false, ct)).Accepted) classified++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Classification failed for transaction {TransactionId}.", tx.Id);
                }
            }
            if (fresh.Count > 0) await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Edenred: {Fetched} fetched, {New} new.", rows.Count, fresh.Count);
        return new EdenredImportResult(rows.Count, fresh.Count, classified);
    }

    /// <summary>
    /// One Edenred connection and account per user. An account the Excel import created for V1's
    /// "Edenred" rows has the same identifier, so it is moved over instead of duplicated.
    /// </summary>
    private async Task<Account> EnsureAccountAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        var connection = await db.BankConnections.SingleOrDefaultAsync(c => c.UserId == userId && c.Provider == BankProvider.Edenred, ct);
        if (connection is null)
        {
            connection = new BankConnection
            {
                Id = Guid.NewGuid(), UserId = userId, Provider = BankProvider.Edenred, BankName = Name, Country = "BE",
                Status = ConnectionStatus.Active, CreatedAt = now, UpdatedAt = now
            };
            db.BankConnections.Add(connection);
        }

        var hash = encryptor.Hash(Name);
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.UserId == userId && a.IdentifierHash == hash, ct);
        if (account is null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(), UserId = userId, Identifier = Name, IdentifierHash = hash, DisplayName = Name,
                Currency = "EUR", CreatedAt = now, UpdatedAt = now
            };
            db.Accounts.Add(account);
        }

        if (account.BankConnectionId != connection.Id)
        {
            account.BankConnectionId = connection.Id;
            account.UpdatedAt = now;
        }
        account.BankConnection = connection;
        return account;
    }

    private static string ExternalId(EdenredOperation op) => "EDENRED-" + op.OperationRef;

    /// <summary>Same text V1 wrote, e.g. "Meal voucher · redemption".</summary>
    private static string Describe(EdenredOperation op)
    {
        var product = op.TransactionDetails?.ProductRef switch
        {
            "TRE" => "Meal voucher",
            "ECE" => "Eco voucher",
            var other => other
        };
        return string.Join(" · ", new[] { product, op.Type, op.Reason }.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private sealed record EdenredOperation(
        [property: JsonPropertyName("operation_ref")] string? OperationRef,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("date")] DateTime Date,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("outlet")] EdenredOutlet? Outlet,
        [property: JsonPropertyName("transaction_details")] EdenredDetails? TransactionDetails);

    private sealed record EdenredOutlet([property: JsonPropertyName("outlet_name")] string? OutletName);

    /// <summary><see cref="Amount"/> is signed cents.</summary>
    private sealed record EdenredDetails(
        [property: JsonPropertyName("product_ref")] string? ProductRef,
        [property: JsonPropertyName("amount")] long Amount);
}
