using BS2.Api.Auth;
using BS2.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace BS2.Api.Endpoints;

public static class MeEndpoints
{
    public static RouteGroupBuilder MapMe(this RouteGroupBuilder api)
    {
        api.MapGet("/me", async (HttpContext context, IAuthorizationService authorization) =>
        {
            var user = (User)context.Items[UserProvisioningMiddleware.ItemKey]!;
            var isAdmin = (await authorization.AuthorizeAsync(context.User, AuthSetup.AdminPolicy)).Succeeded;
            return Results.Ok(new { user.Id, user.Email, user.DisplayName, isAdmin });
        });

        return api;
    }
}
