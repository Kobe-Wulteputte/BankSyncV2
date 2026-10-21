using BS2.Application.Abstractions;
using BS2.Application.Common;
using BS2.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BS2.Application.Groups;

public sealed record GroupDto(Guid Id, string Name, string? Color, int TransactionCount);
public sealed record GroupRequest(string Name, string? Color = null);

public sealed class GroupService(IAppDbContext db, ICurrentUser user, IClock clock)
{
    public async Task<GroupDto[]> ListAsync(CancellationToken ct = default) =>
        await db.Groups.Where(g => g.UserId == user.UserId)
            .OrderBy(g => g.Name)
            .Select(g => new GroupDto(g.Id, g.Name, g.Color, g.Transactions.Count))
            .ToArrayAsync(ct);

    public async Task<GroupDto> CreateAsync(GroupRequest request, CancellationToken ct = default)
    {
        var (name, color) = await ValidateAsync(request, excludeId: null, ct);
        var group = new Group { Id = Guid.NewGuid(), UserId = user.UserId, Name = name, Color = color, CreatedAt = clock.UtcNow };
        db.Groups.Add(group);
        await db.SaveChangesAsync(ct);
        return new GroupDto(group.Id, group.Name, group.Color, 0);
    }

    public async Task<GroupDto> UpdateAsync(Guid id, GroupRequest request, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id && g.UserId == user.UserId, ct)
                    ?? throw new NotFoundException($"Group {id} not found.");
        var (name, color) = await ValidateAsync(request, excludeId: id, ct);
        group.Name = name;
        group.Color = color;
        await db.SaveChangesAsync(ct);
        var count = await db.TransactionGroups.CountAsync(tg => tg.GroupId == id, ct);
        return new GroupDto(group.Id, group.Name, group.Color, count);
    }

    /// <summary>Cascade on transaction_groups detaches it from transactions.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == id && g.UserId == user.UserId, ct)
                    ?? throw new NotFoundException($"Group {id} not found.");
        db.Groups.Remove(group);
        await db.SaveChangesAsync(ct);
    }

    private async Task<(string Name, string? Color)> ValidateAsync(GroupRequest request, Guid? excludeId, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? "";
        var errors = new Dictionary<string, string[]>();
        if (name.Length is 0 or > 128) errors["name"] = ["Name is required (max 128 characters)."];
        else if (await db.Groups.AnyAsync(g => g.UserId == user.UserId && g.Id != excludeId && g.Name.ToLower() == name.ToLower(), ct))
            errors["name"] = [$"A group named '{name}' already exists."];
        if (request.Color is { Length: > 16 }) errors["color"] = ["Color must be at most 16 characters."];
        if (errors.Count > 0) throw new ValidationException(errors);
        return (name, string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim());
    }
}
