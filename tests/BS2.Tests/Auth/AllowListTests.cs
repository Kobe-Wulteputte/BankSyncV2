using System.Security.Claims;
using BS2.Api.Auth;
using Xunit;

namespace BS2.Tests.Auth;

public class AllowListTests
{
    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Theory]
    [InlineData("email", "email_verified")]
    [InlineData("https://banksync/email", "https://banksync/email_verified")]
    public void Verified_email_is_listed(string emailClaim, string verifiedClaim)
    {
        var user = User(("sub", "auth0|x"), (emailClaim, "me@example.com"), (verifiedClaim, "true"));

        Assert.True(user.IsListed([], ["ME@example.com"]));
    }

    [Fact]
    public void Unverified_or_foreign_namespace_email_is_ignored()
    {
        Assert.False(User(("https://banksync/email", "me@example.com"), ("https://banksync/email_verified", "false")).IsListed([], ["me@example.com"]));
        Assert.False(User(("https://banksync/email", "me@example.com")).IsListed([], ["me@example.com"]));
        Assert.False(User(("https://other/email", "me@example.com"), ("https://other/email_verified", "true")).IsListed([], ["me@example.com"]));
    }

    [Fact]
    public void Subject_is_listed_without_email()
    {
        Assert.True(User(("sub", "auth0|x")).IsListed(["auth0|x"], []));
        Assert.False(User(("sub", "auth0|y")).IsListed(["auth0|x"], []));
    }
}
