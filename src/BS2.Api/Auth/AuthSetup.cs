using System.Security.Claims;
using BS2.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BS2.Api.Auth;

public static class AuthSetup
{
    public const string AllowListedPolicy = "AllowListed";

    /// <summary>Changes to data every user shares (categories, classes) and the Excel import.</summary>
    public const string AdminPolicy = "Admin";

    public static IServiceCollection AddAuth0(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<Auth0Options>(configuration.GetSection(Auth0Options.Section));
        var auth0 = configuration.GetSection(Auth0Options.Section).Get<Auth0Options>() ?? new Auth0Options();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{auth0.Domain}/";
                options.Audience = auth0.Audience;
                options.TokenValidationParameters.NameClaimType = ClaimTypes.NameIdentifier;
                options.MapInboundClaims = false; // keep "sub" and "email" as-is
            });

        services.AddSingleton<IAuthorizationHandler, AllowListHandler>();
        services.AddSingleton<IAuthorizationHandler, AdminHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(AllowListedPolicy, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new AllowListRequirement()))
            .AddPolicy(AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new AllowListRequirement(), new AdminRequirement()))
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new AllowListRequirement())
                .Build());

        return services;
    }

    public static string? Subject(this ClaimsPrincipal principal) =>
        principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public const string EmailClaimNamespace = "https://banksync/";

    /// <summary>
    /// The verified email, or null. Auth0 access tokens carry no email unless a post-login Action
    /// adds <c>https://banksync/email</c> and <c>https://banksync/email_verified</c>. An unverified
    /// address is ignored: anyone can sign up with someone else's email.
    /// </summary>
    public static string? Email(this ClaimsPrincipal principal)
    {
        foreach (var prefix in new[] { "", EmailClaimNamespace })
        {
            var email = principal.FindFirstValue(prefix + "email");
            if (email is null) continue;
            var verified = principal.FindFirstValue(prefix + "email_verified");
            return string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase) ? email : null;
        }

        return null;
    }

    public static bool IsListed(this ClaimsPrincipal principal, IEnumerable<string> subjects, IEnumerable<string> emails)
    {
        var subject = principal.Subject();
        var email = principal.Email();
        return (subject is not null && subjects.Contains(subject, StringComparer.Ordinal))
               || (email is not null && emails.Contains(email, StringComparer.OrdinalIgnoreCase));
    }
}

public sealed class AllowListRequirement : IAuthorizationRequirement;

public sealed class AdminRequirement : IAuthorizationRequirement;

/// <summary>
/// Auth0 authenticates anyone with an account in the tenant; this is private data, so only the
/// configured subjects or emails get in. Both lists empty means nobody does, loudly.
/// </summary>
public sealed class AllowListHandler(IOptions<Auth0Options> options, ILogger<AllowListHandler> logger)
    : AuthorizationHandler<AllowListRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AllowListRequirement requirement)
    {
        var auth0 = options.Value;
        if (auth0.AllowedSubjects.Count == 0 && auth0.AllowedEmails.Count == 0)
        {
            logger.LogError("Auth0:AllowedSubjects and Auth0:AllowedEmails are both empty; every request is denied.");
            return Task.CompletedTask;
        }

        if (context.User.IsListed(auth0.AllowedSubjects, auth0.AllowedEmails))
        {
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning("Denied {Subject} ({Email}): not on the allow-list.", context.User.Subject(), context.User.Email() ?? "no verified email claim");
        }

        return Task.CompletedTask;
    }
}

/// <summary>Only <see cref="Auth0Options.AdminSubjects"/> or <see cref="Auth0Options.AdminEmails"/>. Both empty means nobody.</summary>
public sealed class AdminHandler(IOptions<Auth0Options> options) : AuthorizationHandler<AdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        if (context.User.IsListed(options.Value.AdminSubjects, options.Value.AdminEmails)) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
