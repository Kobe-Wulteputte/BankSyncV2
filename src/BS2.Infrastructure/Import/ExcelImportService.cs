using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BS2.Infrastructure.Import;

public sealed record ImportResult(
    int Rows, int Imported, int Updated, int SkippedDuplicates, int GroupsCreated, int AccountsCreated, int CategoriesCreated, IReadOnlyList<string> Errors);

public sealed class ExcelImportService(IAppDbContext db, ICurrentUser user, IClock clock, IFieldEncryptor encryptor, IOptions<ImportOptions> options)
{
    private const int BatchSize = 500;
    private readonly ImportOptions _options = options.Value;

    public async Task<ImportResult> ImportAsync(Stream xlsx, CancellationToken ct = default)
    {
        var read = ExcelWorkbookReader.Read(xlsx);
        var errors = read.Errors.ToList();
        var userId = user.UserId;
        var now = clock.UtcNow;

        var categories = await db.Categories.ToListAsync(ct);
        var accounts = await db.Accounts.Where(a => a.UserId == userId).ToListAsync(ct);
        var connections = await db.BankConnections
            .Where(c => c.UserId == userId && c.Provider == BankProvider.Import).ToListAsync(ct);
        var groups = await db.Groups.Where(g => g.UserId == userId).ToListAsync(ct);
        var groupByName = groups.GroupBy(g => g.Name.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
        // Per user, not per account: an Excel row and a later bank sync of the same entry must not both exist.
        // Existing rows are loaded whole so the workbook's labels can be applied to them (see below).
        var existingById = new Dictionary<string, Transaction>(StringComparer.Ordinal);
        foreach (var t in await db.Transactions.Include(t => t.Groups).Where(t => t.UserId == userId).ToListAsync(ct))
            existingById.TryAdd(t.ExternalId, t);
        var bankAccounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

        int imported = 0, updated = 0, skipped = 0, groupsCreated = 0, accountsCreated = 0, categoriesCreated = 0, pending = 0;
        var reportedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in read.Rows)
        {
            var bankName = ResolveBankName(row.BankName);
            if (bankName != row.BankName && reportedAliases.Add(row.BankName))
                errors.Add($"type '{row.BankName}' imported under bank '{bankName}'");
            var normalized = Iban.Normalize(row.Account);
            var ibanLike = IsIban(normalized);
            // V1's column is DebtorAccount ?? CreditorAccount: on a debit it is the user's own IBAN, on a credit the counterparty's.
            var isDebit = row.Amount < 0;
            var account = ibanLike && isDebit
                ? accounts.FirstOrDefault(a => a.IdentifierHash == encryptor.Hash(normalized))
                : null;
            if (account is null && !bankAccounts.TryGetValue(bankName, out account))
            {
                var hash = encryptor.Hash(bankName);
                var conn = connections.FirstOrDefault(c => string.Equals(c.BankName, bankName, StringComparison.OrdinalIgnoreCase));
                account = conn is null ? null : accounts.FirstOrDefault(a => a.BankConnectionId == conn.Id && a.IdentifierHash == hash);
                if (account is null)
                {
                    if (conn is null)
                    {
                        conn = new BankConnection
                        {
                            Id = Guid.NewGuid(), UserId = userId, Provider = BankProvider.Import, BankName = bankName,
                            Country = "BE", Status = ConnectionStatus.Revoked, CreatedAt = now, UpdatedAt = now
                        };
                        db.BankConnections.Add(conn);
                        connections.Add(conn);
                    }
                    account = new Account
                    {
                        Id = Guid.NewGuid(), UserId = userId, BankConnectionId = conn.Id, Identifier = bankName,
                        IdentifierHash = hash, DisplayName = $"{bankName} (imported)", Currency = "EUR",
                        CreatedAt = now, UpdatedAt = now
                    };
                    db.Accounts.Add(account);
                    accounts.Add(account);
                    accountsCreated++;
                }
                bankAccounts[bankName] = account;
            }

            var externalId = row.ExternalId.Length > 0 ? row.ExternalId : SyntheticId(row);
            if (existingById.TryGetValue(externalId, out var existing) && existing is null) { skipped++; continue; }

            var category = CategorySeed.Resolve(row.Category, categories);
            if (row.Category.Length > 0 && category is null)
            {
                if (_options.CreateMissingCategories)
                {
                    category = CreateCategory(row.Category, categories);
                    categoriesCreated++;
                    errors.Add($"{row.Sheet}!{row.RowNumber}: created category '{category.Name}' ({category.Code})");
                }
                else
                {
                    errors.Add($"{row.Sheet}!{row.RowNumber}: unknown category '{row.Category}'");
                }
            }

            var group = row.Group.Length > 0 ? FindOrCreateGroup(row.Group, groupByName, userId, now, ref groupsCreated) : null;

            if (existing is not null)
            {
                // The workbook was curated by hand in V1, so its labels win over anything the app
                // derived itself (AI, earlier import) but never over a category set manually in the app.
                if (ApplyLabels(existing, category, group, row.Reimbursed, now)) { updated++; pending++; }
                else skipped++;
                if (pending >= BatchSize) { await db.SaveChangesAsync(ct); pending = 0; }
                continue;
            }

            var tx = new Transaction
            {
                Id = Guid.NewGuid(), UserId = userId, AccountId = account.Id, Source = TransactionSource.ExcelImport,
                ExternalId = externalId, Amount = row.Amount, Currency = "EUR", Date = row.Date, BookingDate = row.Date,
                CounterpartyName = row.Name, CounterpartyIban = ibanLike && !isDebit ? row.Account : null, Description = row.Description,
                RawJson = JsonSerializer.Serialize(row), Category = category, // navigation, not CategoryId: a category created above has no id yet
                ClassificationSource = category is null ? ClassificationSource.None : ClassificationSource.Import,
                ClassifiedAt = category is null ? null : now, Reimbursed = row.Reimbursed, CreatedAt = now, UpdatedAt = now
            };
            db.Transactions.Add(tx);
            existingById[externalId] = tx;
            if (group is not null)
                db.TransactionGroups.Add(new TransactionGroup { TransactionId = tx.Id, GroupId = group.Id, CreatedAt = now });

            imported++;
            if (++pending >= BatchSize) { await db.SaveChangesAsync(ct); pending = 0; }
        }

        await db.SaveChangesAsync(ct);
        return new ImportResult(read.Rows.Count, imported, updated, skipped, groupsCreated, accountsCreated, categoriesCreated, errors);
    }

    private Group FindOrCreateGroup(string name, Dictionary<string, Group> groupByName, Guid userId, DateTime now, ref int created)
    {
        var key = name.ToLowerInvariant();
        if (groupByName.TryGetValue(key, out var group)) return group;
        group = new Group { Id = Guid.NewGuid(), UserId = userId, Name = name, CreatedAt = now };
        db.Groups.Add(group);
        groupByName[key] = group;
        created++;
        return group;
    }

    /// <summary>
    /// Applies workbook labels to a transaction that already exists (synced or imported earlier).
    /// Category: only when the workbook has one and the app's value was not set manually.
    /// Group: added, never removed. Reimbursed: set when the workbook says so, never cleared.
    /// Returns true when anything changed.
    /// </summary>
    private bool ApplyLabels(Transaction existing, Category? category, Group? group, bool reimbursed, DateTime now)
    {
        var changed = false;

        if (category is not null && existing.ClassificationSource != ClassificationSource.Manual
            && (existing.CategoryId != category.Id || existing.Category?.Code != category.Code || existing.ClassificationSource != ClassificationSource.Import))
        {
            existing.Category = category;
            existing.CategoryId = category.Id == 0 ? existing.CategoryId : category.Id;
            existing.ClassificationSource = ClassificationSource.Import;
            existing.ClassifiedAt = now;
            changed = true;
        }

        if (group is not null && existing.Groups.All(tg => tg.GroupId != group.Id))
        {
            db.TransactionGroups.Add(new TransactionGroup { TransactionId = existing.Id, GroupId = group.Id, CreatedAt = now });
            changed = true;
        }

        if (reimbursed && !existing.Reimbursed)
        {
            existing.Reimbursed = true;
            changed = true;
        }

        if (changed) existing.UpdatedAt = now;
        return changed;
    }

    /// <summary>Alias table first, then <c>NAME_BIC</c> provider ids, else the value itself.</summary>
    private string ResolveBankName(string type)
    {
        var trimmed = type.Trim();
        if (trimmed.Length == 0) return "Unknown";
        if (_options.BankAliases.TryGetValue(trimmed, out var alias) && !string.IsNullOrWhiteSpace(alias)) return alias.Trim();

        var underscore = trimmed.IndexOf('_');
        if (underscore > 0 && trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && trimmed == trimmed.ToUpperInvariant())
        {
            var name = trimmed[..underscore];
            return char.ToUpperInvariant(name[0]) + name[1..].ToLowerInvariant();
        }

        return trimmed;
    }

    /// <summary>
    /// New categories get an Expense kind; the user can change it in Settings. The code is the
    /// alphanumeric form of the name, which is also what the classifier prompt will list.
    /// </summary>
    private Category CreateCategory(string name, List<Category> categories)
    {
        var trimmed = name.Trim();
        var code = new string(trimmed.Where(char.IsAsciiLetterOrDigit).ToArray());
        if (code.Length == 0) code = "Category" + (categories.Count + 1);
        var baseCode = code;
        for (var i = 2; categories.Any(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)); i++) code = baseCode + i;

        var category = new Category
        {
            Code = code, Name = trimmed, Kind = CategoryKind.Expense, IsActive = true,
            SortOrder = (categories.Count == 0 ? 0 : categories.Max(c => c.SortOrder)) + 1
        };
        db.Categories.Add(category);
        categories.Add(category);
        return category;
    }

    private static bool IsIban(string s) =>
        s.Length >= 15 && char.IsAsciiLetter(s[0]) && char.IsAsciiLetter(s[1]) && char.IsAsciiDigit(s[2]) && char.IsAsciiDigit(s[3]);

    private static string SyntheticId(ImportedRow r)
    {
        var raw = $"{r.BankName}|{r.Date:yyyy-MM-dd}|{r.Amount.ToString(CultureInfo.InvariantCulture)}|{r.Name}|{r.Description}";
        return ("IMPORT-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant())[..64];
    }
}
