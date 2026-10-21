namespace BS2.Domain.Enums;

/// <summary>
/// Drives the income/expense/savings split. Sign of the amount decides direction; kind decides
/// whether the row participates at all (transfers between own accounts never do).
/// </summary>
public enum CategoryKind
{
    Expense = 0,
    Income = 1,
    Transfer = 2
}

public enum ClassificationSource
{
    None = 0,
    Ai = 1,
    Manual = 2,
    Import = 3
}

public enum ClassificationTrigger
{
    Sync = 0,
    Manual = 1,
    Import = 2,
    Backfill = 3
}

public enum TransactionSource
{
    EnableBanking = 0,
    Edenred = 1,
    ExcelImport = 2
}

public enum BankProvider
{
    EnableBanking = 0,
    Edenred = 1,
    Import = 2
}

public enum ConnectionStatus
{
    /// <summary>Configured but never authorized.</summary>
    NotAuthorized = 0,
    Active = 1,
    Expired = 2,
    Revoked = 3
}

public enum SyncStatus
{
    Running = 0,
    Succeeded = 1,
    PartiallySucceeded = 2,
    Failed = 3
}

public enum SyncTrigger
{
    Scheduled = 0,
    Manual = 1
}
