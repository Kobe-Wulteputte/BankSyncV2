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

    /// <summary>Probability of the emitted sequence (exp of summed token logprobs), 0..1.</summary>
    public double? Confidence { get; set; }

    public double Threshold { get; set; }
    public bool Accepted { get; set; }

    /// <summary>Top alternatives for the first token, as JSON: [{"code":..,"logprob":..,"probability":..}].</summary>
    public string? AlternativesJson { get; set; }

    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int LatencyMs { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
}
