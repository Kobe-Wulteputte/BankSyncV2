using BS2.Domain.Entities;

namespace BS2.Application.Classification;

public sealed record ClassificationExample(Transaction Transaction, Category Category);

/// <summary>A group the transaction might belong to: its members' date span and some of their counterparties.</summary>
public sealed record GroupCandidate(Guid Id, string Name, DateOnly From, DateOnly To, IReadOnlyList<string> Counterparties);

/// <summary>What the classifier gets besides the transaction itself.</summary>
public sealed record ClassificationContext(IReadOnlyList<ClassificationExample> Examples, IReadOnlyList<GroupCandidate> Groups)
{
    public static readonly ClassificationContext Empty = new([], []);
}

public sealed record HistoryMatch(Category Category, int Agreeing, int Total)
{
    public double Share => (double)Agreeing / Total;
}

/// <summary>
/// A user's classified transactions and groups, held in memory: counterparty and description are
/// encrypted, so matching cannot run in SQL. Pure.
/// </summary>
public sealed class TransactionHistory
{
    /// <summary>Earlier transactions with the same counterparty that must agree before the model is skipped.</summary>
    public const double MinAgreement = 0.9;
    private const int MinMatches = 2;
    private const int GroupWindowDays = 30;
    private const int MaxGroups = 10;
    private const double SameIbanScore = 10; // outweighs any word overlap

    private sealed record Item(Transaction Tx, Category Category, string Iban, string Name, HashSet<string> Words);

    private readonly List<Item> _items;
    private readonly Dictionary<string, int> _documentFrequency = [];
    private readonly IReadOnlyList<Group> _groups;

    /// <param name="classified">Transactions with a category. Ones whose category is not in <paramref name="categories"/> are ignored.</param>
    /// <param name="groups">Groups with <c>Transactions.Transaction</c> loaded.</param>
    public TransactionHistory(IEnumerable<Transaction> classified, IReadOnlyList<Category> categories, IReadOnlyList<Group> groups)
    {
        var byId = categories.ToDictionary(c => c.Id);
        _items = classified
            .Where(t => t.CategoryId is { } id && byId.ContainsKey(id))
            .Select(t => new Item(t, byId[t.CategoryId!.Value], NormalizeIban(t.CounterpartyIban), NormalizeName(t.CounterpartyName), Words(t)))
            .ToList();
        foreach (var word in _items.SelectMany(i => i.Words))
            _documentFrequency[word] = _documentFrequency.GetValueOrDefault(word) + 1;
        _groups = groups;
    }

    /// <summary>
    /// The category earlier transactions with the same IBAN, else the same counterparty name, agree on.
    /// IBAN goes first but does not block the name: payment processors share one IBAN across merchants.
    /// </summary>
    public HistoryMatch? Match(Transaction tx)
    {
        var iban = NormalizeIban(tx.CounterpartyIban);
        var name = NormalizeName(tx.CounterpartyName);
        return Agree(_items.Where(i => i.Tx.Id != tx.Id && iban.Length > 0 && i.Iban == iban).ToList())
               ?? Agree(_items.Where(i => i.Tx.Id != tx.Id && name.Length > 0 && i.Name == name).ToList());
    }

    /// <summary>
    /// Most similar earlier transactions: same IBAN first, then shared words weighted by rarity (IDF),
    /// so a store name counts and card-terminal boilerplate shared by everything does not.
    /// </summary>
    public IReadOnlyList<ClassificationExample> Similar(Transaction tx, int count)
    {
        // ponytail: linear scan per transaction; move to embeddings/pgvector if history grows past ~50k rows.
        var iban = NormalizeIban(tx.CounterpartyIban);
        var words = Words(tx);
        double total = _items.Count;
        return _items
            .Where(i => i.Tx.Id != tx.Id)
            .Select(i => (Item: i, Score: (iban.Length > 0 && i.Iban == iban ? SameIbanScore : 0)
                                          + words.Where(i.Words.Contains).Sum(w => Math.Log(total / _documentFrequency[w]))))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Item.Tx.Date)
            .Take(count)
            .Select(x => new ClassificationExample(x.Item.Tx, x.Item.Category))
            .ToList();
    }

    /// <summary>Groups with a member within 30 days, or one with the same counterparty. Those with the same counterparty first.</summary>
    public IReadOnlyList<GroupCandidate> CandidateGroups(Transaction tx)
    {
        var iban = NormalizeIban(tx.CounterpartyIban);
        var name = NormalizeName(tx.CounterpartyName);
        bool SameCounterparty(Transaction m) =>
            (iban.Length > 0 && NormalizeIban(m.CounterpartyIban) == iban) || (name.Length > 0 && NormalizeName(m.CounterpartyName) == name);

        return _groups
            .Select(g => (Group: g, Members: g.Transactions.Select(m => m.Transaction).Where(m => m.Id != tx.Id).ToList()))
            .Where(x => x.Members.Count > 0)
            .Select(x => (x.Group, x.Members,
                Same: x.Members.Any(SameCounterparty),
                Distance: x.Members.Min(m => Math.Abs(m.Date.DayNumber - tx.Date.DayNumber))))
            .Where(x => x.Same || x.Distance <= GroupWindowDays)
            .OrderByDescending(x => x.Same)
            .ThenBy(x => x.Distance)
            .Take(MaxGroups)
            .Select(x => new GroupCandidate(
                x.Group.Id, x.Group.Name, x.Members.Min(m => m.Date), x.Members.Max(m => m.Date),
                x.Members.OrderByDescending(m => m.Date).Select(m => m.CounterpartyName.Trim())
                    .Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList()))
            .ToList();
    }

    private static HistoryMatch? Agree(List<Item> same)
    {
        if (same.Count < MinMatches) return null;
        var top = same.GroupBy(i => i.Category).MaxBy(g => g.Count())!;
        var match = new HistoryMatch(top.Key, top.Count(), same.Count);
        return match.Share >= MinAgreement ? match : null;
    }

    private static string NormalizeIban(string? iban) =>
        new((iban ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>Letters only, so "DELHAIZE 123" and "Delhaize-123" match; store numbers are dropped.</summary>
    private static string NormalizeName(string? name) =>
        string.Join(' ', SplitWords(name));

    private static HashSet<string> Words(Transaction t) =>
        SplitWords(t.CounterpartyName).Concat(SplitWords(t.Description)).Where(w => w.Length >= 3).ToHashSet();

    private static IEnumerable<string> SplitWords(string? text) =>
        new string((text ?? "").Select(c => char.IsLetter(c) ? char.ToUpperInvariant(c) : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
