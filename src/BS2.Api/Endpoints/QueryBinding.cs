using System.Globalization;
using BS2.Application.Transactions;

namespace BS2.Api.Endpoints;

/// <summary>Parses the common filter from the query string. Lists accept repeats and comma-separated values.</summary>
public static class QueryBinding
{
    public static TransactionFilter BindFilter(IQueryCollection q) => new(
        From: Date(q, "from"),
        To: Date(q, "to"),
        AccountIds: Guids(q, "accountIds"),
        CategoryIds: Ints(q, "categoryIds"),
        ExcludeCategoryIds: Ints(q, "excludeCategoryIds"),
        GroupIds: Guids(q, "groupIds"),
        ExcludeGroupIds: Guids(q, "excludeGroupIds"),
        ExcludeReimbursed: Bool(q, "excludeReimbursed"),
        Search: string.IsNullOrWhiteSpace(q["search"]) ? null : q["search"].ToString().Trim());

    public static bool Bool(IQueryCollection q, string key, bool fallback = false) =>
        bool.TryParse(q[key], out var value) ? value : fallback;

    public static int Int(IQueryCollection q, string key, int fallback) =>
        int.TryParse(q[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private static DateOnly? Date(IQueryCollection q, string key) =>
        DateOnly.TryParseExact(q[key], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static IEnumerable<string> Values(IQueryCollection q, string key) =>
        q[key].SelectMany(v => (v ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static Guid[]? Guids(IQueryCollection q, string key)
    {
        var list = Values(q, key).Select(v => Guid.TryParse(v, out var g) ? g : (Guid?)null).Where(g => g.HasValue).Select(g => g!.Value).ToArray();
        return list.Length == 0 ? null : list;
    }

    private static int[]? Ints(IQueryCollection q, string key)
    {
        var list = Values(q, key).Select(v => int.TryParse(v, out var i) ? i : (int?)null).Where(i => i.HasValue).Select(i => i!.Value).ToArray();
        return list.Length == 0 ? null : list;
    }
}
