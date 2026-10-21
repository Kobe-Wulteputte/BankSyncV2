using BS2.Domain.Entities;

namespace BS2.Application.Transactions;

/// <summary>Common filter shared by the transaction list and every analytics endpoint. Null/empty = not applied.</summary>
public sealed record TransactionFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    Guid[]? AccountIds = null,
    int[]? CategoryIds = null,
    int[]? ExcludeCategoryIds = null,
    Guid[]? GroupIds = null,
    Guid[]? ExcludeGroupIds = null,
    bool ExcludeReimbursed = false,
    string? Search = null)
{
    public bool HasSearch => !string.IsNullOrWhiteSpace(Search);

    /// <summary>Every predicate that can run in SQL. <see cref="Search"/> is not one of them: counterparty and description are ciphertext.</summary>
    public static IQueryable<Transaction> Apply(IQueryable<Transaction> q, TransactionFilter f, Guid userId)
    {
        q = q.Where(t => t.UserId == userId);

        if (f.From is { } from) q = q.Where(t => t.Date >= from);
        if (f.To is { } to) q = q.Where(t => t.Date <= to);
        if (f.AccountIds is { Length: > 0 } accounts) q = q.Where(t => accounts.Contains(t.AccountId));

        if (f.CategoryIds is { Length: > 0 })
        {
            var includeNull = f.CategoryIds.Contains(0);
            var ids = f.CategoryIds.Where(i => i != 0).ToArray();
            q = q.Where(t => (includeNull && t.CategoryId == null) || (t.CategoryId != null && ids.Contains(t.CategoryId.Value)));
        }

        if (f.ExcludeCategoryIds is { Length: > 0 })
        {
            var excludeNull = f.ExcludeCategoryIds.Contains(0);
            var ids = f.ExcludeCategoryIds.Where(i => i != 0).ToArray();
            q = q.Where(t => !(excludeNull && t.CategoryId == null) && !(t.CategoryId != null && ids.Contains(t.CategoryId.Value)));
        }

        if (f.GroupIds is { Length: > 0 } groups) q = q.Where(t => t.Groups.Any(g => groups.Contains(g.GroupId)));
        if (f.ExcludeGroupIds is { Length: > 0 } excluded) q = q.Where(t => !t.Groups.Any(g => excluded.Contains(g.GroupId)));
        if (f.ExcludeReimbursed) q = q.Where(t => !t.Reimbursed);

        return q;
    }

    /// <summary>In-memory part, after EF has decrypted the rows.</summary>
    public static IEnumerable<Transaction> ApplySearch(IEnumerable<Transaction> rows, string? search) =>
        string.IsNullOrWhiteSpace(search) ? rows : rows.Where(t => Matches(search, t.CounterpartyName, t.Description));

    public static bool Matches(string search, string? counterpartyName, string? description) =>
        (counterpartyName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
        || (description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
}
