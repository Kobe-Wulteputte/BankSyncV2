using BS2.Domain.Enums;

namespace BS2.Domain.Entities;

public class SyncRun
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public SyncTrigger Trigger { get; set; }
    public SyncStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int ConnectionsSynced { get; set; }
    public int ConnectionsSkipped { get; set; }
    public int TransactionsFetched { get; set; }
    public int TransactionsNew { get; set; }
    public int TransactionsClassified { get; set; }
    public string? Error { get; set; }

    /// <summary>Per-connection detail as JSON, for the UI.</summary>
    public string? DetailsJson { get; set; }
}
