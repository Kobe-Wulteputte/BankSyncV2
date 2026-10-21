using System.Text.Json;
using BS2.Domain.Entities;
using BS2.Domain.Enums;

namespace BS2.Application.Sync;

/// <summary><c>Status</c> is "Synced", "Skipped" or "Failed".</summary>
public sealed record SyncDetailDto(string BankName, string Status, string? Message, int Fetched, int New);

public sealed record SyncRunDto(
    Guid Id,
    SyncTrigger Trigger,
    SyncStatus Status,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int ConnectionsSynced,
    int ConnectionsSkipped,
    int TransactionsFetched,
    int TransactionsNew,
    int TransactionsClassified,
    string? Error,
    IReadOnlyList<SyncDetailDto> Details)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static SyncRunDto From(SyncRun run) => new(
        run.Id, run.Trigger, run.Status, run.StartedAt, run.FinishedAt,
        run.ConnectionsSynced, run.ConnectionsSkipped, run.TransactionsFetched, run.TransactionsNew, run.TransactionsClassified,
        run.Error,
        run.DetailsJson is null ? [] : JsonSerializer.Deserialize<List<SyncDetailDto>>(run.DetailsJson, Json) ?? []);
}

public sealed record SyncStatusDto(bool Running, SyncRunDto? LastRun, DateTime? NextScheduledAt);

/// <summary>Filled by the background worker so the UI can show when the next scheduled sync runs.</summary>
public interface ISyncSchedule
{
    DateTime? NextRunUtc { get; }
}

/// <summary>Default when no scheduler is registered (tests, Interval = 0).</summary>
public sealed class NoSyncSchedule : ISyncSchedule
{
    public DateTime? NextRunUtc => null;
}
