using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Transactions;

public sealed class TransactionQueryService(IAppDbContext db, ICurrentUser user)
{
    public const int MaxPageSize = 500;

    public async Task<PagedResult<TransactionDto>> ListAsync(
        TransactionFilter filter, bool unclassifiedOnly, int page, int pageSize, string? sort, string? dir, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        sort = sort?.ToLowerInvariant() ?? "date";
        var desc = dir is null ? sort == "date" : string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);

        var q = TransactionFilter.Apply(db.Transactions, filter, user.UserId);
        if (unclassifiedOnly) q = q.Where(t => t.CategoryId == null);

        // Counterparty is ciphertext, so searching or sorting on it means pulling the SQL-filtered set into memory.
        if (filter.HasSearch || sort == "counterpartyname")
        {
            var all = await Project(q).ToListAsync(ct);
            if (filter.HasSearch) all = all.Where(r => TransactionFilter.Matches(filter.Search!, r.CounterpartyName, r.Description)).ToList();
            var pageRows = SortInMemory(all, sort, desc).Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto).ToList();
            return new PagedResult<TransactionDto>(pageRows, all.Count, page, pageSize, all.Sum(r => r.Amount));
        }

        var total = await q.CountAsync(ct);
        var sum = total == 0 ? 0m : await q.SumAsync(t => t.Amount, ct);
        var items = await Project(SortInSql(q, sort, desc).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new PagedResult<TransactionDto>(items.Select(ToDto).ToList(), total, page, pageSize, sum);
    }

    public async Task<TransactionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await Project(db.Transactions.Where(t => t.Id == id && t.UserId == user.UserId)).FirstOrDefaultAsync(ct)
                  ?? throw new NotFoundException($"Transaction {id} not found.");
        return ToDto(row);
    }

    private static IQueryable<Transaction> SortInSql(IQueryable<Transaction> q, string sort, bool desc) => (sort, desc) switch
    {
        ("amount", true) => q.OrderByDescending(t => t.Amount).ThenByDescending(t => t.Date),
        ("amount", false) => q.OrderBy(t => t.Amount).ThenBy(t => t.Date),
        ("category", true) => q.OrderByDescending(t => t.Category!.Name).ThenByDescending(t => t.Date),
        ("category", false) => q.OrderBy(t => t.Category!.Name).ThenBy(t => t.Date),
        (_, true) => q.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt),
        (_, false) => q.OrderBy(t => t.Date).ThenBy(t => t.CreatedAt)
    };

    private static IEnumerable<Row> SortInMemory(IEnumerable<Row> rows, string sort, bool desc) => (sort, desc) switch
    {
        ("amount", true) => rows.OrderByDescending(r => r.Amount).ThenByDescending(r => r.Date),
        ("amount", false) => rows.OrderBy(r => r.Amount).ThenBy(r => r.Date),
        ("category", true) => rows.OrderByDescending(r => r.CategoryName).ThenByDescending(r => r.Date),
        ("category", false) => rows.OrderBy(r => r.CategoryName).ThenBy(r => r.Date),
        ("counterpartyname", true) => rows.OrderByDescending(r => r.CounterpartyName, StringComparer.OrdinalIgnoreCase).ThenByDescending(r => r.Date),
        ("counterpartyname", false) => rows.OrderBy(r => r.CounterpartyName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Date),
        (_, true) => rows.OrderByDescending(r => r.Date).ThenByDescending(r => r.CreatedAt),
        (_, false) => rows.OrderBy(r => r.Date).ThenBy(r => r.CreatedAt)
    };

    // Encrypted columns are projected one by one so the value converter runs per column; no COALESCE over ciphertext.
    private static IQueryable<Row> Project(IQueryable<Transaction> q) => q.Select(t => new Row
    {
        Id = t.Id,
        AccountId = t.AccountId,
        AccountDisplayName = t.Account.DisplayName,
        AccountIdentifier = t.Account.Identifier,
        BankName = t.Account.BankConnection.BankName,
        Source = t.Source,
        Date = t.Date,
        BookingDate = t.BookingDate,
        ValueDate = t.ValueDate,
        Amount = t.Amount,
        Currency = t.Currency,
        CounterpartyName = t.CounterpartyName,
        CounterpartyIban = t.CounterpartyIban,
        Description = t.Description,
        Notes = t.Notes,
        CategoryId = t.CategoryId,
        CategoryName = t.Category != null ? t.Category.Name : null,
        ClassificationSource = t.ClassificationSource,
        ClassifiedAt = t.ClassifiedAt,
        Reimbursed = t.Reimbursed,
        CreatedAt = t.CreatedAt,
        GroupIds = t.Groups.Select(g => g.GroupId).ToList(),
        LatestClassification = t.ClassificationRuns
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new LatestClassificationDto(r.PredictedCategoryId, r.Confidence, r.Accepted, r.CreatedAt))
            .FirstOrDefault()
    });

    private static TransactionDto ToDto(Row r) => new(
        r.Id, r.AccountId, r.AccountDisplayName ?? r.AccountIdentifier, r.BankName, r.Source,
        r.Date, r.BookingDate, r.ValueDate, r.Amount, r.Currency,
        r.CounterpartyName, r.CounterpartyIban, r.Description, r.Notes,
        r.CategoryId, r.ClassificationSource, r.ClassifiedAt, r.Reimbursed,
        r.GroupIds.ToArray(), r.LatestClassification);

    private sealed class Row
    {
        public Guid Id { get; init; }
        public Guid AccountId { get; init; }
        public string? AccountDisplayName { get; init; }
        public string AccountIdentifier { get; init; } = "";
        public string BankName { get; init; } = "";
        public TransactionSource Source { get; init; }
        public DateOnly Date { get; init; }
        public DateOnly? BookingDate { get; init; }
        public DateOnly? ValueDate { get; init; }
        public decimal Amount { get; init; }
        public string Currency { get; init; } = "";
        public string CounterpartyName { get; init; } = "";
        public string? CounterpartyIban { get; init; }
        public string Description { get; init; } = "";
        public string? Notes { get; init; }
        public int? CategoryId { get; init; }
        public string? CategoryName { get; init; }
        public ClassificationSource ClassificationSource { get; init; }
        public DateTime? ClassifiedAt { get; init; }
        public bool Reimbursed { get; init; }
        public DateTime CreatedAt { get; init; }
        public List<Guid> GroupIds { get; init; } = [];
        public LatestClassificationDto? LatestClassification { get; init; }
    }
}
