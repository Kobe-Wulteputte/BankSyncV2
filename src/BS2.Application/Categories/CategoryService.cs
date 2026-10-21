using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Categories;

public sealed record CategoryDto(int Id, string Code, string Name, CategoryKind Kind, int SortOrder, bool IsActive, string? Color, int? CategoryClassId);
public sealed record CreateCategoryRequest(string Code, string Name, CategoryKind Kind, string? Color = null, int? CategoryClassId = null);
public sealed record UpdateCategoryRequest(string Name, CategoryKind Kind, string? Color, bool IsActive, int SortOrder, int? CategoryClassId);

/// <summary>Categories are global (the model was trained on their codes), so nothing here scopes by user.</summary>
public sealed class CategoryService(IAppDbContext db)
{
    public async Task<CategoryDto[]> ListAsync(CancellationToken ct = default) =>
        (await db.Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync(ct)).Select(ToDto).ToArray();

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var code = request.Code?.Trim() ?? "";
        if (code.Length is 0 or > 64 || !code.All(char.IsAsciiLetterOrDigit)) errors["code"] = ["Code must be 1-64 letters or digits."];
        else if (await db.Categories.AnyAsync(c => c.Code.ToLower() == code.ToLower(), ct)) errors["code"] = [$"Code '{code}' already exists."];
        ValidateCommon(request.Name, request.Kind, request.Color, errors);
        await ValidateClassAsync(request.CategoryClassId, errors, ct);
        if (errors.Count > 0) throw new ValidationException(errors);

        var maxSort = await db.Categories.Select(c => (int?)c.SortOrder).MaxAsync(ct) ?? 0;
        var category = new Category
        {
            Code = code, Name = request.Name.Trim(), Kind = request.Kind, Color = Clean(request.Color), SortOrder = maxSort + 1, IsActive = true,
            CategoryClassId = request.CategoryClassId
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException($"Category {id} not found.");

        var errors = new Dictionary<string, string[]>();
        ValidateCommon(request.Name, request.Kind, request.Color, errors);
        await ValidateClassAsync(request.CategoryClassId, errors, ct);
        if (errors.Count > 0) throw new ValidationException(errors);

        category.Name = request.Name.Trim();
        category.Kind = request.Kind;
        category.Color = Clean(request.Color);
        category.IsActive = request.IsActive;
        category.SortOrder = request.SortOrder;
        category.CategoryClassId = request.CategoryClassId;
        await db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    private async Task ValidateClassAsync(int? classId, Dictionary<string, string[]> errors, CancellationToken ct)
    {
        if (classId is { } id && !await db.CategoryClasses.AnyAsync(c => c.Id == id, ct))
            errors["categoryClassId"] = [$"Category class {id} does not exist."];
    }

    private static void ValidateCommon(string? name, CategoryKind kind, string? color, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 128) errors["name"] = ["Name is required (max 128 characters)."];
        if (!Enum.IsDefined(kind)) errors["kind"] = ["Kind must be Expense, Income or Transfer."];
        if (color is { Length: > 16 }) errors["color"] = ["Color must be at most 16 characters."];
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static CategoryDto ToDto(Category c) => new(c.Id, c.Code, c.Name, c.Kind, c.SortOrder, c.IsActive, c.Color, c.CategoryClassId);
}
