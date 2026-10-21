using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Categories;

public sealed record CategoryClassDto(int Id, string Name, string? Color, int SortOrder, int CategoryCount);
public sealed record CreateCategoryClassRequest(string Name, string? Color = null);
public sealed record UpdateCategoryClassRequest(string Name, string? Color, int SortOrder);

/// <summary>Category classes are global, like categories, so nothing here scopes by user.</summary>
public sealed class CategoryClassService(IAppDbContext db)
{
    public async Task<CategoryClassDto[]> ListAsync(CancellationToken ct = default) =>
        (await db.CategoryClasses
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new CategoryClassDto(c.Id, c.Name, c.Color, c.SortOrder, c.Categories.Count))
            .ToListAsync(ct)).ToArray();

    public async Task<CategoryClassDto> CreateAsync(string name, string? color, CancellationToken ct = default)
    {
        await ValidateAsync(name, color, null, ct);
        var maxSort = await db.CategoryClasses.Select(c => (int?)c.SortOrder).MaxAsync(ct) ?? -1;
        var entity = new CategoryClass { Name = name.Trim(), Color = Clean(color), SortOrder = maxSort + 1 };
        db.CategoryClasses.Add(entity);
        await db.SaveChangesAsync(ct);
        return new CategoryClassDto(entity.Id, entity.Name, entity.Color, entity.SortOrder, 0);
    }

    public async Task<CategoryClassDto> UpdateAsync(int id, string name, string? color, int sortOrder, CancellationToken ct = default)
    {
        var entity = await db.CategoryClasses.FirstOrDefaultAsync(c => c.Id == id, ct)
                     ?? throw new NotFoundException($"Category class {id} not found.");
        await ValidateAsync(name, color, id, ct);
        entity.Name = name.Trim();
        entity.Color = Clean(color);
        entity.SortOrder = sortOrder;
        await db.SaveChangesAsync(ct);
        var count = await db.Categories.CountAsync(c => c.CategoryClassId == id, ct);
        return new CategoryClassDto(entity.Id, entity.Name, entity.Color, entity.SortOrder, count);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await db.CategoryClasses.Include(c => c.Categories).FirstOrDefaultAsync(c => c.Id == id, ct)
                     ?? throw new NotFoundException($"Category class {id} not found.");
        // Explicit null-out: InMemory (tests) has no ON DELETE SET NULL.
        foreach (var c in entity.Categories) c.CategoryClassId = null;
        db.CategoryClasses.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateAsync(string? name, string? color, int? selfId, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length is 0 or > 64) errors["name"] = ["Name is required (max 64 characters)."];
        else if (await db.CategoryClasses.AnyAsync(c => c.Id != selfId && c.Name.ToLower() == trimmed.ToLower(), ct))
            errors["name"] = [$"Name '{trimmed}' already exists."];
        if (color is { Length: > 16 }) errors["color"] = ["Color must be at most 16 characters."];
        if (errors.Count > 0) throw new ValidationException(errors);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
