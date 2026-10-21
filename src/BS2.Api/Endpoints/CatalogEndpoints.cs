using BS2.Api.Auth;
using BS2.Application.Banking;
using BS2.Application.Categories;
using BS2.Application.Groups;

namespace BS2.Api.Endpoints;

/// <summary>Categories, groups and accounts: small CRUD surfaces.</summary>
public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalog(this RouteGroupBuilder api)
    {
        var categories = api.MapGroup("/categories");
        categories.MapGet("/", async (CategoryService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));
        categories.MapPost("/", async (CreateCategoryRequest request, CategoryService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/categories/{created.Id}", created);
        }).RequireAuthorization(AuthSetup.AdminPolicy);
        categories.MapPut("/{id:int}", async (int id, UpdateCategoryRequest request, CategoryService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct))).RequireAuthorization(AuthSetup.AdminPolicy);

        var classes = api.MapGroup("/category-classes");
        classes.MapGet("/", async (CategoryClassService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));
        classes.MapPost("/", async (CreateCategoryClassRequest request, CategoryClassService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request.Name, request.Color, ct);
            return Results.Created($"/api/category-classes/{created.Id}", created);
        }).RequireAuthorization(AuthSetup.AdminPolicy);
        classes.MapPut("/{id:int}", async (int id, UpdateCategoryClassRequest request, CategoryClassService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request.Name, request.Color, request.SortOrder, ct))).RequireAuthorization(AuthSetup.AdminPolicy);
        classes.MapDelete("/{id:int}", async (int id, CategoryClassService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        }).RequireAuthorization(AuthSetup.AdminPolicy);

        var groups = api.MapGroup("/groups");
        groups.MapGet("/", async (GroupService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));
        groups.MapPost("/", async (GroupRequest request, GroupService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/groups/{created.Id}", created);
        });
        groups.MapPut("/{id:guid}", async (Guid id, GroupRequest request, GroupService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct)));
        groups.MapDelete("/{id:guid}", async (Guid id, GroupService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        var accounts = api.MapGroup("/accounts");
        accounts.MapGet("/", async (AccountService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));
        accounts.MapPut("/{id:guid}", async (Guid id, UpdateAccountRequest request, AccountService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct)));

        return api;
    }
}
