using System.Diagnostics;
using Betalgo.Ranul.OpenAI.Interfaces;
using Betalgo.Ranul.OpenAI.ObjectModels;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Betalgo.Ranul.OpenAI.ObjectModels.SharedModels;
using BS2.Application;
using BS2.Application.Abstractions;
using BS2.Application.Classification;
using BS2.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BS2.Infrastructure.Classification;

/// <summary>
/// General chat model with similar earlier transactions as examples. A strict JSON schema limits the answer
/// to known category codes; the confidence is the model's own estimate, since newer models do not return logprobs.
/// </summary>
public sealed class OpenAiTransactionClassifier : ITransactionClassifier
{
    private readonly IOpenAIService _openAi;
    private readonly ClassificationOptions _options;
    private readonly ILogger<OpenAiTransactionClassifier> _logger;

    public OpenAiTransactionClassifier(IOpenAIService openAi, IOptions<ClassificationOptions> options, ILogger<OpenAiTransactionClassifier> logger)
    {
        _openAi = openAi;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAsync(Transaction transaction, IReadOnlyList<Category> categories, ClassificationContext context, CancellationToken ct)
    {
        var systemPrompt = ClassificationPrompt.System(categories);
        // Callers must Include Account.BankConnection; the fallbacks only keep the prompt well-formed.
        var bankName = transaction.Account?.BankConnection?.BankName ?? transaction.Account?.DisplayName ?? "";
        var userPrompt = ClassificationPrompt.User(transaction, bankName, context);
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(_options.Model))
            return Failed(systemPrompt, userPrompt, sw, "Classification:Model is not configured.");

        try
        {
            var response = await _openAi.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Model = _options.Model,
                Messages = [ChatMessage.FromSystem(systemPrompt), ChatMessage.FromUser(userPrompt)],
                ResponseFormat = new ResponseFormat
                {
                    Type = StaticValues.CompletionStatics.ResponseFormat.JsonSchema,
                    JsonSchema = new JsonSchema { Name = "classification", Strict = true, Schema = Schema(categories, context.Groups.Count) }
                }
            }, cancellationToken: ct);
            sw.Stop();

            if (!response.Successful)
            {
                var message = response.Error?.Message ?? $"OpenAI returned HTTP {(int)response.HttpStatusCode}";
                _logger.LogError("Classification failed for transaction {TransactionId}: {Error}", transaction.Id, message);
                return Failed(systemPrompt, userPrompt, sw, message);
            }

            var raw = response.Choices.FirstOrDefault()?.Message?.Content;
            var parsed = ClassificationResponseParser.Parse(raw, categories, context.Groups);
            return new ClassificationResult(
                _options.Model, systemPrompt, userPrompt, raw,
                parsed.PredictedCode, parsed.Category, parsed.Confidence, _options.Threshold,
                ClassificationResponseParser.Decide(parsed.Confidence, _options.Threshold, parsed.Category),
                parsed.Alternatives,
                response.Usage?.PromptTokens, response.Usage?.CompletionTokens,
                (int)sw.ElapsedMilliseconds, null, parsed.SuggestedGroupId);
        }
        catch (Exception e)
        {
            sw.Stop();
            _logger.LogError(e, "Classification threw for transaction {TransactionId}", transaction.Id);
            return Failed(systemPrompt, userPrompt, sw, e.Message);
        }
    }

    /// <summary>Strict mode: every property required, no extras. <c>group</c> only exists when there are candidates.</summary>
    private static PropertyDefinition Schema(IReadOnlyList<Category> categories, int groupCount)
    {
        var codes = categories.Select(c => c.Code).ToList();
        var properties = new Dictionary<string, PropertyDefinition>
        {
            ["category"] = PropertyDefinition.DefineEnum(codes),
            ["confidence"] = PropertyDefinition.DefineNumber("Probability between 0 and 1 that the category is right"),
            ["alternatives"] = PropertyDefinition.DefineArray(PropertyDefinition.DefineObject(
                new Dictionary<string, PropertyDefinition>
                {
                    ["category"] = PropertyDefinition.DefineEnum(codes),
                    ["confidence"] = PropertyDefinition.DefineNumber()
                },
                ["category", "confidence"], false, null, null))
        };
        if (groupCount > 0)
            properties["group"] = PropertyDefinition.DefineEnum(Enumerable.Range(0, groupCount + 1).Select(i => i.ToString()).ToList(), "Group number, 0 for none");

        return PropertyDefinition.DefineObject(properties, properties.Keys.ToList(), false, null, null);
    }

    private ClassificationResult Failed(string systemPrompt, string userPrompt, Stopwatch sw, string error) =>
        new(_options.Model ?? "", systemPrompt, userPrompt, null, null, null, null, _options.Threshold, false, [],
            null, null, (int)sw.ElapsedMilliseconds, error);
}
