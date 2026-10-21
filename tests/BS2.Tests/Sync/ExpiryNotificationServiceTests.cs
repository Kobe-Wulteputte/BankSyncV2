using Xunit;
using BS2.Application.Banking;
using BS2.Application.Sync;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using BS2.Tests.Banking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace BS2.Tests.Sync;

public class ExpiryNotificationServiceTests
{
    private readonly FakeClock _clock = new();
    private readonly FakeEncryptor _encryptor = new();
    private readonly FakeBankingProvider _provider = new();
    private readonly FakeEmailSender _email = new();

    [Fact]
    public async Task Emails_once_per_created_link_and_only_for_connections_needing_authorization()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        db.Users.Add(new User { Id = TestEnv.UserId, Email = "owner@example.com" });
        db.AddRange(
            TestEnv.NewConnection(_clock, "Fresh", status: ConnectionStatus.NotAuthorized, sessionId: null),
            TestEnv.NewConnection(_clock, "Expiring", validUntil: _clock.UtcNow.AddHours(12)),
            TestEnv.NewConnection(_clock, "Healthy", validUntil: _clock.UtcNow.AddDays(30)));
        await db.SaveChangesAsync();

        var options = TestEnv.SyncOptions(o => { o.NotifyEmail = "me@example.com"; o.RenewBeforeDays = 1; o.AuthorizationLinkTtlHours = 24; });
        var flow = new AuthorizationFlowService(db, _provider, _encryptor, _clock, options, TestEnv.AppOptions(), NullLogger<AuthorizationFlowService>.Instance);
        var sut = new ExpiryNotificationService(db, flow, _email, _clock, options, NullLogger<ExpiryNotificationService>.Instance);

        Assert.Equal(2, await sut.RunForUserAsync(TestEnv.UserId, CancellationToken.None));
        Assert.Equal(["BankSync: authorize Expiring", "BankSync: authorize Fresh"], _email.Sent.Select(m => m.Subject).Order());
        Assert.All(_email.Sent, m => Assert.Equal("owner@example.com", m.To));
        Assert.Contains("https://bank.example/auth?state=", _email.Sent[0].Html);
        Assert.DoesNotContain("callback service", _email.Sent[0].Html);

        // Same links still outstanding: nothing new goes out.
        Assert.Equal(0, await sut.RunForAllUsersAsync(CancellationToken.None));
        Assert.Equal(2, _email.Sent.Count);
        Assert.Equal(2, await db.PendingAuthorizations.CountAsync());

        // Links went stale: one fresh mail per connection again.
        _clock.UtcNow = _clock.UtcNow.AddHours(25);
        Assert.Equal(2, await sut.RunForUserAsync(TestEnv.UserId, CancellationToken.None));
        Assert.Equal(4, _email.Sent.Count);
        Assert.Equal(2, await db.PendingAuthorizations.CountAsync());
    }

    [Fact]
    public async Task Falls_back_to_configured_address_when_user_has_no_email()
    {
        await using var db = TestEnv.NewDb(_encryptor);
        db.Users.Add(new User { Id = TestEnv.UserId, Email = "" });
        db.Add(TestEnv.NewConnection(_clock, "Fresh", status: ConnectionStatus.NotAuthorized, sessionId: null));
        await db.SaveChangesAsync();

        var options = TestEnv.SyncOptions(o => o.NotifyEmail = "fallback@example.com");
        var flow = new AuthorizationFlowService(db, _provider, _encryptor, _clock, options, TestEnv.AppOptions(), NullLogger<AuthorizationFlowService>.Instance);
        var sut = new ExpiryNotificationService(db, flow, _email, _clock, options, NullLogger<ExpiryNotificationService>.Instance);

        await sut.RunForUserAsync(TestEnv.UserId, CancellationToken.None);

        Assert.Equal("fallback@example.com", Assert.Single(_email.Sent).To);
    }
}
