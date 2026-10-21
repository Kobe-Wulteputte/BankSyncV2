using BS2.Domain.Enums;

namespace BS2.Application.Transactions;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize, decimal SumAmount);

public sealed record TransactionDto(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string BankName,
    TransactionSource Source,
    DateOnly Date,
    DateOnly? BookingDate,
    DateOnly? ValueDate,
    decimal Amount,
    string Currency,
    string CounterpartyName,
    string? CounterpartyIban,
    string Description,
    string? Notes,
    int? CategoryId,
    ClassificationSource ClassificationSource,
    DateTime? ClassifiedAt,
    bool Reimbursed,
    Guid[] GroupIds,
    LatestClassificationDto? LatestClassification);

public sealed record LatestClassificationDto(int? PredictedCategoryId, double? Confidence, bool Accepted, DateTime CreatedAt);

public sealed record ClassificationRunDto(
    Guid Id,
    ClassificationTrigger Trigger,
    string Model,
    DateTime CreatedAt,
    int? PredictedCategoryId,
    string? PredictedCategoryCode,
    double? Confidence,
    double Threshold,
    bool Accepted,
    ClassificationAlternativeDto[] Alternatives,
    int? PromptTokens,
    int? CompletionTokens,
    int LatencyMs,
    string? Error,
    string SystemPrompt,
    string UserPrompt,
    string? RawResponse);

public sealed record ClassificationAlternativeDto(string Code, double Logprob, double Probability);

/// <summary>
/// <paramref name="CategorySpecified"/> distinguishes "categoryId absent" (leave as is) from
/// "categoryId: null" (clear). A plain nullable cannot express that after JSON binding.
/// <paramref name="NotesSpecified"/> does the same for notes: when set, null clears and a string replaces.
/// </summary>
public sealed record PatchTransactionRequest(
    bool CategorySpecified = false,
    int? CategoryId = null,
    bool? Reimbursed = null,
    string? Notes = null,
    Guid[]? GroupIds = null,
    bool NotesSpecified = false);

public sealed record BulkRequest(
    Guid[] Ids,
    bool CategorySpecified = false,
    int? CategoryId = null,
    bool? Reimbursed = null,
    Guid[]? AddGroupIds = null,
    Guid[]? RemoveGroupIds = null);
