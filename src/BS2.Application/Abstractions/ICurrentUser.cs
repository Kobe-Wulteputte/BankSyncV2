namespace BS2.Application.Abstractions;

/// <summary>
/// The authenticated user for the current request, or the user a background job runs on behalf of.
/// Every query scopes by <see cref="UserId"/>; nothing reads another user's rows.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string Auth0Subject { get; }
    string Email { get; }
}

/// <summary>Fixed user, for background workers and CLI commands.</summary>
public sealed class StaticCurrentUser(Guid userId, string auth0Subject = "", string email = "") : ICurrentUser
{
    public Guid UserId { get; } = userId;
    public string Auth0Subject { get; } = auth0Subject;
    public string Email { get; } = email;
}
