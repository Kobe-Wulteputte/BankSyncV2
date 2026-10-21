using System.Text.Json;
using BS2.Application.Common;
using BS2.Application.Transactions;

namespace BS2.Api.Endpoints;

public static class TransactionEndpoints
{
    public static RouteGroupBuilder MapTransactions(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/transactions");

        group.MapGet("/", async (HttpRequest request, TransactionQueryService queries, CancellationToken ct) =>
        {
            var q = request.Query;
            var result = await queries.ListAsync(
                QueryBinding.BindFilter(q),
                QueryBinding.Bool(q, "unclassifiedOnly"),
                QueryBinding.Int(q, "page", 1),
                QueryBinding.Int(q, "pageSize", 50),
                q["sort"],
                q["dir"],
                ct);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, TransactionQueryService queries, CancellationToken ct) =>
            Results.Ok(await queries.GetAsync(id, ct)));

        // Body bound as JSON so "categoryId absent" and "categoryId: null" stay distinguishable.
        group.MapPatch("/{id:guid}", async (Guid id, JsonElement body, TransactionCommandService commands, CancellationToken ct) =>
        {
            RequireObject(body);
            var request = new PatchTransactionRequest(
                CategorySpecified: body.TryGetProperty("categoryId", out _),
                CategoryId: NullableInt(body, "categoryId"),
                Reimbursed: NullableBool(body, "reimbursed"),
                NotesSpecified: body.TryGetProperty("notes", out _),
                Notes: NullableString(body, "notes"),
                GroupIds: Guids(body, "groupIds"));
            return Results.Ok(await commands.PatchAsync(id, request, ct));
        });

        group.MapPost("/bulk", async (JsonElement body, TransactionCommandService commands, CancellationToken ct) =>
        {
            RequireObject(body);
            var request = new BulkRequest(
                Ids: Guids(body, "ids") ?? [],
                CategorySpecified: body.TryGetProperty("categoryId", out _),
                CategoryId: NullableInt(body, "categoryId"),
                Reimbursed: NullableBool(body, "reimbursed"),
                AddGroupIds: Guids(body, "addGroupIds"),
                RemoveGroupIds: Guids(body, "removeGroupIds"));
            var updated = await commands.BulkAsync(request, ct);
            return Results.Ok(new { updated });
        });

        group.MapGet("/{id:guid}/classifications", async (Guid id, TransactionCommandService commands, CancellationToken ct) =>
            Results.Ok(await commands.GetClassificationsAsync(id, ct)));

        group.MapPost("/{id:guid}/classify", async (Guid id, HttpRequest request, TransactionCommandService commands, CancellationToken ct) =>
            Results.Ok(await commands.ClassifyNowAsync(id, QueryBinding.Bool(request.Query, "force"), ct)));

        return api;
    }

    private static void RequireObject(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
            throw new ValidationException("body", "Expected a JSON object.");
    }

    private static int? NullableInt(JsonElement body, string name)
    {
        if (!body.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var value)) return value;
        throw new ValidationException(name, "Expected an integer or null.");
    }

    private static bool? NullableBool(JsonElement body, string name)
    {
        if (!body.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
        if (p.ValueKind is JsonValueKind.True or JsonValueKind.False) return p.GetBoolean();
        throw new ValidationException(name, "Expected a boolean.");
    }

    private static string? NullableString(JsonElement body, string name)
    {
        if (!body.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
        if (p.ValueKind == JsonValueKind.String) return p.GetString();
        throw new ValidationException(name, "Expected a string or null.");
    }

    private static Guid[]? Guids(JsonElement body, string name)
    {
        if (!body.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null) return null;
        if (p.ValueKind != JsonValueKind.Array) throw new ValidationException(name, "Expected an array of ids.");

        var ids = new List<Guid>();
        foreach (var element in p.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String || !Guid.TryParse(element.GetString(), out var id))
                throw new ValidationException(name, "Expected an array of GUID strings.");
            ids.Add(id);
        }

        return ids.ToArray();
    }
}
