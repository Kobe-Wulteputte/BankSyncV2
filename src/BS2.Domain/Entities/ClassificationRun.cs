using BS2.Domain.Enums;

namespace BS2.Domain.Entities;

/// <summary>
/// Full record of one model call. Kept even when the prediction is rejected or later overridden,
/// so model quality and threshold choice can be analysed afterwards.
/// </summary>
public class ClassificationRun
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public Transaction Transaction { get; set; } = null!;

    public ClassificationTrigger Trigger { get; set; }
    public string Model { get; set; } = string.Empty;

    [Encrypted]
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>Contains counterparty and description, hence encrypted.</summary>
    [Encrypted]
    public string UserPrompt { get; set; } = string.Empty;

    /// <summary>Verbatim completion text.</summary>
    [Encrypted]
    public string? RawResponse { get; set; }

    /// <summary>Category code parsed from the response, even if not accepted.</summary>
    public string? PredictedCategoryCode { get; set; }

    public int? PredictedCategoryId { get; set; }
    public Category? PredictedCategory { get; set; }

    /// <summary>
    /// 0..1. The model's own estimate; for <c>history</c> runs the share of agreeing earlier transactions.
    /// Runs from the old fine-tuned model hold the sequence probability (exp of summed token logprobs).
    /// </summary>
    public double? Confidence { get; set; }

    public double Threshold { get; set; }
    public bool Accepted { get; set; }

    /// <summary>Alternative categories, as JSON: [{"code":..,"logprob":..,"probability":..}].</summary>
    public string? AlternativesJson { get; set; }

    /// <summary>Group the model thinks the transaction belongs to. Only a suggestion: never applied automatically.</summary>
    public Guid? SuggestedGroupId { get; set; }
    public Group? SuggestedGroup { get; set; }

    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int LatencyMs { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
}
