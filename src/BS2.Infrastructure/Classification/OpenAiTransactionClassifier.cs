using System.Diagnostics;
using Betalgo.Ranul.OpenAI.Interfaces;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using BS2.Application;
using BS2.Application.Abstractions;
using BS2.Application.Classification;
using BS2.Application.Common;
using BS2.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BS2.Infrastructure.Classification;

/// <summary>V1's fine-tuned chat completion call, plus top-logprobs so alternatives can be stored.</summary>
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

    public async Task<ClassificationResult> ClassifyAsync(Transaction transaction, IReadOnlyList<Category> categories, CancellationToken ct)
    {
        var systemPrompt = TransactionRules.BuildSystemPrompt(categories);
        // Callers must Include Account.BankConnection; the fallbacks only keep the prompt well-formed.
        var bankName = transaction.Account?.BankConnection?.BankName ?? transaction.Account?.DisplayName ?? "";
        var userPrompt = TransactionRules.BuildAiPrompt(transaction, bankName);
        var sw = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(_options.Model))
            return Failed(systemPrompt, userPrompt, sw, "Classification:Model is not configured.");

        try
        {
            var response = await _openAi.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Model = _options.Model,
                Messages = [ChatMessage.FromSystem(systemPrompt), ChatMessage.FromUser(userPrompt)],
                MaxTokens = 10,
                Temperature = 0,
                Stop = " END",
                LogProbs = true,
                TopLogprobs = _options.TopLogProbs
            }, cancellationToken: ct);
            sw.Stop();

            if (!response.Successful)
            {
                var message = response.Error?.Message ?? $"OpenAI returned HTTP {(int)response.HttpStatusCode}";
                _logger.LogError("Classification failed for transaction {TransactionId}: {Error}", transaction.Id, message);
                return Failed(systemPrompt, userPrompt, sw, message);
            }

            var choice = response.Choices.FirstOrDefault();
            var raw = choice?.Message?.Content;
            var tokens = choice?.LogProbs?.Content?
                .Select(c => new TokenLogProb(
                    c.Token,
                    c.LogProb,
                    c.TopLogProbs?.Select(a => (a.Token, a.LogProb)).ToList() ?? []))
                .ToList() ?? [];

            var parsed = ClassificationResponseParser.Parse(raw, categories, tokens);
            return new ClassificationResult(
                _options.Model, systemPrompt, userPrompt, raw,
                parsed.PredictedCode, parsed.Category, parsed.Confidence, _options.Threshold,
                ClassificationResponseParser.Decide(parsed.Confidence, _options.Threshold, parsed.Category),
                parsed.Alternatives,
                response.Usage?.PromptTokens, response.Usage?.CompletionTokens,
                (int)sw.ElapsedMilliseconds, null);
        }
        catch (Exception e)
        {
            sw.Stop();
            _logger.LogError(e, "Classification threw for transaction {TransactionId}", transaction.Id);
            return Failed(systemPrompt, userPrompt, sw, e.Message);
        }
    }

    private ClassificationResult Failed(string systemPrompt, string userPrompt, Stopwatch sw, string error) =>
        new(_options.Model ?? "", systemPrompt, userPrompt, null, null, null, null, _options.Threshold, false, [],
            null, null, (int)sw.ElapsedMilliseconds, error);
}
