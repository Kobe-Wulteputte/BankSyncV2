using BS2.Application.Abstractions;
using BS2.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BS2.Api.Auth;

/// <summary>
/// Resolves the authenticated principal to a <see cref="User"/> row once per request, creating it
/// on first login. Runs after authorization and skips anonymous endpoints, so only allow-listed
/// users ever get a row.
/// </summary>
public sealed class UserProvisioningMiddleware(RequestDelegate next, ILogger<UserProvisioningMiddleware> logger)
{
    public const string ItemKey = "bs2.user";

    public async Task InvokeAsync(HttpContext context, IAppDbContext db, IClock clock)
    {
        var endpoint = context.GetEndpoint();
        var anonymous = endpoint is null || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var subject = context.User.Subject();

        if (!anonymous && context.User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(subject))
        {
            context.Items[ItemKey] = await ProvisionAsync(db, clock, subject, context.User.Email() ?? string.Empty, context.RequestAborted);
        }

        await next(context);
    }

    private async Task<User> ProvisionAsync(IAppDbContext db, IClock clock, string subject, string email, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Auth0Subject == subject, ct);

        if (user is null)
        {
            user = new User { Id = Guid.NewGuid(), Auth0Subject = subject, Email = email, CreatedAt = now, LastLoginAt = now };
            db.Users.Add(user);
            try
            {
                await db.SaveChangesAsync(ct);
                return user;
            }
            catch (DbUpdateException)
            {
                // First login fires several parallel requests; whichever lost the unique index race
                // re-reads the row the winner inserted.
                logger.LogDebug("Concurrent first-login insert for {Subject}; re-reading.", subject);
                if (db is DbContext dbContext) dbContext.Entry(user).State = EntityState.Detached;
                return await db.Users.FirstAsync(u => u.Auth0Subject == subject, ct);
            }
        }

        // Throttled so a page load full of requests does not write the row every time.
        var emailChanged = email.Length > 0 && user.Email != email;
        if (emailChanged || user.LastLoginAt is null || now - user.LastLoginAt > TimeSpan.FromHours(1))
        {
            user.LastLoginAt = now;
            if (email.Length > 0) user.Email = email;
            await db.SaveChangesAsync(ct);
        }

        return user;
    }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private User Current =>
        accessor.HttpContext?.Items[UserProvisioningMiddleware.ItemKey] as User
        ?? throw new InvalidOperationException("No authenticated user on this request.");

    public Guid UserId => Current.Id;
    public string Auth0Subject => Current.Auth0Subject;
    public string Email => Current.Email;
}
