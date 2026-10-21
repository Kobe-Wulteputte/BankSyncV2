using BS2.Application.Abstractions;
using BS2.Application.Transactions;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Analytics;

public sealed record SummaryDto(int TransactionCount, int UnclassifiedCount, DateOnly? FirstDate, DateOnly? LastDate, DateTime? LastSyncAt);

public sealed class AnalyticsService(IAppDbContext db, ICurrentUser user)
{
    public async Task<ExpensesResult> ExpensesAsync(TransactionFilter filter, CancellationToken ct = default) =>
        AnalyticsCalculator.Expenses(await RowsAsync(filter, ct), await db.Categories.Include(c => c.CategoryClass).ToListAsync(ct));

    public async Task<IncomeResult> IncomeAsync(TransactionFilter filter, CancellationToken ct = default) =>
        AnalyticsCalculator.Income(await RowsAsync(filter, ct), filter.From, filter.To, await db.Categories.Include(c => c.CategoryClass).ToListAsync(ct));

    public async Task<SavingsResult> SavingsAsync(TransactionFilter filter, CancellationToken ct = default) =>
        AnalyticsCalculator.Savings(await RowsAsync(filter, ct), filter.From, filter.To);

    public Task<SummaryDto> SummaryAsync(CancellationToken ct = default) => SummaryAsync(new TransactionFilter(), ct);

    /// <summary>Counts and date range follow the filter; <c>LastSyncAt</c> stays user-wide.</summary>
    public async Task<SummaryDto> SummaryAsync(TransactionFilter filter, CancellationToken ct = default)
    {
        var mine = TransactionFilter.Apply(db.Transactions, filter, user.UserId);
        var lastSync = await db.SyncRuns.Where(r => r.UserId == user.UserId).MaxAsync(r => r.FinishedAt, ct);

        if (!filter.HasSearch)
            return new SummaryDto(
                await mine.CountAsync(ct),
                await mine.CountAsync(t => t.CategoryId == null, ct),
                await mine.OrderBy(t => t.Date).Select(t => (DateOnly?)t.Date).FirstOrDefaultAsync(ct),
                await mine.OrderByDescending(t => t.Date).Select(t => (DateOnly?)t.Date).FirstOrDefaultAsync(ct),
                lastSync);

        // Search runs on decrypted text, so it has to happen in memory.
        var rows = (await mine.Select(t => new { t.Date, t.CategoryId, t.CounterpartyName, t.Description }).ToListAsync(ct))
            .Where(r => TransactionFilter.Matches(filter.Search!, r.CounterpartyName, r.Description))
            .ToList();
        return new SummaryDto(
            rows.Count,
            rows.Count(r => r.CategoryId == null),
            rows.Count == 0 ? null : rows.Min(r => r.Date),
            rows.Count == 0 ? null : rows.Max(r => r.Date),
            lastSync);
    }

    private async Task<List<AnalyticsRow>> RowsAsync(TransactionFilter filter, CancellationToken ct)
    {
        var q = TransactionFilter.Apply(db.Transactions, filter, user.UserId);

        if (!filter.HasSearch)
            return await q.Select(t => new AnalyticsRow(
                t.Date, t.Amount, t.CategoryId, t.Category != null ? t.Category.Kind : (CategoryKind?)null, t.Reimbursed)).ToListAsync(ct);

        var rows = await q.Select(t => new
        {
            t.Date, t.Amount, t.CategoryId, Kind = t.Category != null ? t.Category.Kind : (CategoryKind?)null, t.Reimbursed,
            t.CounterpartyName, t.Description
        }).ToListAsync(ct);

        return rows
            .Where(r => TransactionFilter.Matches(filter.Search!, r.CounterpartyName, r.Description))
            .Select(r => new AnalyticsRow(r.Date, r.Amount, r.CategoryId, r.Kind, r.Reimbursed))
            .ToList();
    }
}
