using System.Text.Json;
using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Transactions;

public sealed class TransactionCommandService(
    IAppDbContext db,
    ICurrentUser user,
    IClock clock,
    TransactionQueryService queries,
    ITransactionClassificationService classifier)
{
    private const int MaxNotesLength = 2000;

    private static readonly JsonSerializerOptions AlternativesJson = new(JsonSerializerDefaults.Web);

    public async Task<TransactionDto> PatchAsync(Guid id, PatchTransactionRequest request, CancellationToken ct = default)
    {
        var tx = await db.Transactions.Include(t => t.Groups)
                     .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.UserId, ct)
                 ?? throw new NotFoundException($"Transaction {id} not found.");

        if (request.CategorySpecified)
        {
            if (request.CategoryId is { } categoryId) await EnsureCategoryAsync(categoryId, ct);
            SetCategory(tx, request.CategoryId);
        }
        if (request.Reimbursed is { } reimbursed) tx.Reimbursed = reimbursed;
        if (request.NotesSpecified)
        {
            var notes = request.Notes?.Trim();
            if (notes is { Length: > MaxNotesLength }) throw new ValidationException("notes", $"Notes can be at most {MaxNotesLength} characters.");
            tx.Notes = string.IsNullOrEmpty(notes) ? null : notes;
        }

        if (request.GroupIds is not null)
        {
            var wanted = request.GroupIds.Distinct().ToArray();
            await EnsureGroupsAsync(wanted, ct);
            foreach (var tg in tx.Groups.Where(g => !wanted.Contains(g.GroupId)).ToList()) tx.Groups.Remove(tg);
            foreach (var gid in wanted.Where(w => tx.Groups.All(g => g.GroupId != w)))
                tx.Groups.Add(new TransactionGroup { TransactionId = tx.Id, GroupId = gid, CreatedAt = clock.UtcNow });
        }

        tx.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await queries.GetAsync(id, ct);
    }

    public async Task<int> BulkAsync(BulkRequest request, CancellationToken ct = default)
    {
        if (request.Ids is not { Length: > 0 }) return 0;

        if (request.CategorySpecified && request.CategoryId is { } categoryId) await EnsureCategoryAsync(categoryId, ct);
        var add = request.AddGroupIds?.Distinct().ToArray() ?? [];
        var remove = request.RemoveGroupIds?.Distinct().ToArray() ?? [];
        await EnsureGroupsAsync(add, ct);

        var ids = request.Ids.Distinct().ToArray();
        var txs = await db.Transactions.Include(t => t.Groups)
            .Where(t => t.UserId == user.UserId && ids.Contains(t.Id))
            .ToListAsync(ct);

        foreach (var tx in txs)
        {
            if (request.CategorySpecified) SetCategory(tx, request.CategoryId);
            if (request.Reimbursed is { } reimbursed) tx.Reimbursed = reimbursed;
            foreach (var tg in tx.Groups.Where(g => remove.Contains(g.GroupId)).ToList()) tx.Groups.Remove(tg);
            foreach (var gid in add.Where(a => tx.Groups.All(g => g.GroupId != a)))
                tx.Groups.Add(new TransactionGroup { TransactionId = tx.Id, GroupId = gid, CreatedAt = clock.UtcNow });
            tx.UpdatedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return txs.Count;
    }

    public async Task<ClassificationRunDto[]> GetClassificationsAsync(Guid id, CancellationToken ct = default)
    {
        if (!await db.Transactions.AnyAsync(t => t.Id == id && t.UserId == user.UserId, ct))
            throw new NotFoundException($"Transaction {id} not found.");

        var runs = await db.ClassificationRuns.Where(r => r.TransactionId == id).OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return runs.Select(ToDto).ToArray();
    }

    public async Task<ClassificationRunDto> ClassifyNowAsync(Guid id, bool force, CancellationToken ct = default)
    {
        var tx = await db.Transactions
                     .Include(t => t.Account).ThenInclude(a => a.BankConnection)
                     .FirstOrDefaultAsync(t => t.Id == id && t.UserId == user.UserId, ct)
                 ?? throw new NotFoundException($"Transaction {id} not found.");

        var run = await classifier.ClassifyAsync(tx, ClassificationTrigger.Manual, force, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(run);
    }

    private void SetCategory(Transaction tx, int? categoryId)
    {
        tx.CategoryId = categoryId;
        tx.ClassificationSource = categoryId is null ? ClassificationSource.None : ClassificationSource.Manual;
        tx.ClassifiedAt = categoryId is null ? null : clock.UtcNow;
    }

    private async Task EnsureCategoryAsync(int categoryId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive, ct))
            throw new ValidationException("categoryId", $"Category {categoryId} does not exist or is inactive.");
    }

    private async Task EnsureGroupsAsync(Guid[] groupIds, CancellationToken ct)
    {
        if (groupIds.Length == 0) return;
        var known = await db.Groups.Where(g => g.UserId == user.UserId && groupIds.Contains(g.Id)).CountAsync(ct);
        if (known != groupIds.Length) throw new ValidationException("groupIds", "One or more groups do not exist.");
    }

    public static ClassificationRunDto ToDto(ClassificationRun r) => new(
        r.Id, r.Trigger, r.Model, r.CreatedAt,
        r.PredictedCategoryId, r.PredictedCategoryCode, r.Confidence, r.Threshold, r.Accepted,
        ParseAlternatives(r.AlternativesJson),
        r.PromptTokens, r.CompletionTokens, r.LatencyMs, r.Error,
        r.SystemPrompt, r.UserPrompt, r.RawResponse);

    private static ClassificationAlternativeDto[] ParseAlternatives(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<ClassificationAlternativeDto[]>(json, AlternativesJson) ?? []; }
        catch (JsonException) { return []; }
    }
}
