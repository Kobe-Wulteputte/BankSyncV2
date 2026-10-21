using BS2.Application;
using BS2.Application.Banking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BS2.Api.Endpoints;

public static class BankEndpoints
{
    public static RouteGroupBuilder MapBanks(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/banks");

        group.MapGet("/aspsps", async (string? country, BankConnectionService service, CancellationToken ct) =>
            Results.Ok(await service.GetBanksAsync(string.IsNullOrWhiteSpace(country) ? "BE" : country.ToUpperInvariant(), ct)));

        group.MapGet("/connections", async (BankConnectionService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(ct)));

        group.MapGet("/connections/{id:guid}", async (Guid id, BankConnectionService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(id, ct)));

        group.MapPost("/connections", async (UpsertBankConnectionRequest request, BankConnectionService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/banks/connections/{created.Id}", created);
        });

        group.MapPut("/connections/{id:guid}", async (Guid id, UpsertBankConnectionRequest request, BankConnectionService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync(id, request, ct)));

        group.MapDelete("/connections/{id:guid}", async (Guid id, BankConnectionService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        group.MapPost("/connections/{id:guid}/authorize", async (Guid id, BankConnectionService service, CancellationToken ct) =>
            Results.Ok(await service.StartAuthorizationAsync(id, ct)));

        group.MapPost("/connections/{id:guid}/refresh", async (Guid id, BankConnectionService service, CancellationToken ct) =>
            Results.Ok(await service.RefreshAsync(id, ct)));

        // The bank redirects the browser here without any of our tokens, so this is anonymous. It
        // only hands code and state to the SPA, which completes the authorization as the signed-in
        // user. Completing here would let anyone who opens a forwarded link attach their bank
        // session to the connection of whoever started it.
        group.MapGet("/callback", (
                string? code,
                string? state,
                string? error,
                IOptions<AppOptions> app,
                ILogger<AuthorizationFlowService> logger) =>
            {
                var origin = string.IsNullOrWhiteSpace(app.Value.ClientOrigin) ? "" : app.Value.ClientOrigin.TrimEnd('/');
                Dictionary<string, string?> query;
                if (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                {
                    logger.LogWarning("Bank callback without a code: {Error}", error);
                    // Fixed text: the error parameter is attacker-controlled and would be shown on our origin.
                    query = new() { ["result"] = "error", ["message"] = "The bank did not complete the authorization." };
                }
                else
                {
                    // Not "code"/"state": the Auth0 SDK treats those on any SPA URL as its own login callback.
                    query = new() { ["bankCode"] = code, ["bankState"] = state };
                }

                return Results.Redirect($"{origin}/banks{QueryString.Create(query)}");
            })
            .AllowAnonymous()
            .WithMetadata(new AllowAnonymousAttribute());

        group.MapPost("/callback", async (CompleteAuthorizationRequest request, BankConnectionService service, CancellationToken ct) =>
        {
            var outcome = await service.CompleteAuthorizationAsync(request.Code, request.State, ct);
            return Results.Ok(new { outcome.Success, bank = outcome.BankName, outcome.Message });
        });

        return api;
    }
}
