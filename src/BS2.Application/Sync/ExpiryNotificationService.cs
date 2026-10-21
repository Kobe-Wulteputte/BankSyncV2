using System.Net;
using BS2.Application.Abstractions;
using BS2.Application.Banking;
using BS2.Domain.Entities;
using BS2.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BS2.Application.Sync;

/// <summary>
/// Emails an authorization link for every connection that is unauthorized, expired or expiring
/// within <see cref="SyncOptions.RenewBeforeDays"/>. One mail per link: a mail goes out only when
/// the <see cref="PendingAuthorization"/> row was created in this call, so repeated runs stay quiet
/// until the link itself goes stale.
/// </summary>
public sealed class ExpiryNotificationService(
    IAppDbContext db,
    AuthorizationFlowService flow,
    IEmailSender emailSender,
    IClock clock,
    IOptions<SyncOptions> options,
    ILogger<ExpiryNotificationService> logger)
{
    public Task<int> RunForUserAsync(Guid userId, CancellationToken ct) => RunAsync(userId, ct);

    public Task<int> RunForAllUsersAsync(CancellationToken ct) => RunAsync(null, ct);

    /// <summary>Returns the number of links created (and therefore mailed, when mail is configured).</summary>
    private async Task<int> RunAsync(Guid? userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var cutoff = now.AddDays(options.Value.RenewBeforeDays);

        var query = db.BankConnections.Where(c => c.Provider == BankProvider.EnableBanking
                                                   && (c.Status == ConnectionStatus.NotAuthorized
                                                       || c.Status == ConnectionStatus.Expired
                                                       || (c.Status == ConnectionStatus.Active && c.ValidUntil != null && c.ValidUntil < cutoff)));
        if (userId is { } uid) query = query.Where(c => c.UserId == uid);
        var connections = await query.OrderBy(c => c.BankName).ToListAsync(ct);

        var created = 0;
        foreach (var connection in connections)
        {
            ct.ThrowIfCancellationRequested();
            PendingAuthorization pending;
            bool isNew;
            try
            {
                (pending, isNew) = await flow.EnsurePendingAsync(connection, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogError(ex, "{Bank}: could not start authorization.", connection.BankName);
                continue;
            }

            if (!isNew)
            {
                logger.LogInformation("{Bank}: authorization link already sent {Created:u}; not sending again yet.", connection.BankName, pending.CreatedAt);
                continue;
            }

            created++;
            logger.LogWarning("{Bank}: needs authorization.", connection.BankName);

            var userEmail = (await db.Users.Where(u => u.Id == connection.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct))?.Trim();
            var recipient = string.IsNullOrEmpty(userEmail) ? options.Value.NotifyEmail : userEmail;
            if (string.IsNullOrWhiteSpace(recipient))
            {
                logger.LogWarning("{Bank}: needs authorization but neither the user nor Sync:NotifyEmail has an email; open the link from the Banks page.", connection.BankName);
                continue;
            }

            try
            {
                await emailSender.SendAsync(recipient, $"BankSync: authorize {connection.BankName}",
                    BuildHtml(connection, pending.Url), BuildText(connection, pending.Url), ct);
                logger.LogInformation("{Bank}: authorization link emailed to {Recipient}.", connection.BankName, recipient);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // The link is stored as pending either way; it stays reachable from the Banks page.
                logger.LogError(ex, "{Bank}: could not email the authorization link.", connection.BankName);
            }
        }

        return created;
    }

    /// <summary>Table layout with inline styles: mail clients strip stylesheets. Everything is HTML-encoded.</summary>
    internal static string BuildHtml(BankConnection connection, string rawUrl)
    {
        var bank = WebUtility.HtmlEncode(connection.BankName);
        var country = WebUtility.HtmlEncode(connection.Country);
        var url = WebUtility.HtmlEncode(rawUrl);

        return $"""
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#f4f5f7;margin:0;padding:24px 0;">
                  <tr>
                    <td align="center">
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:520px;background:#ffffff;border:1px solid #e2e5e9;border-radius:8px;font-family:-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
                        <tr>
                          <td style="padding:24px 28px 0 28px;">
                            <div style="font-size:12px;letter-spacing:0.08em;text-transform:uppercase;color:#8a9099;">BankSync</div>
                            <div style="margin-top:6px;font-size:20px;line-height:1.3;font-weight:600;color:#1b1f24;">Authorize {bank}</div>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:14px 28px 0 28px;font-size:15px;line-height:1.55;color:#3c4149;">
                            Your {bank} ({country}) connection needs re-authorization before transactions can sync again.
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:22px 28px;">
                            <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                              <tr>
                                <td style="border-radius:6px;background:#1f6feb;">
                                  <a href="{url}" style="display:inline-block;padding:11px 22px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;">Authorize {bank}</a>
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:0 28px 24px 28px;font-size:13px;line-height:1.55;color:#6b7280;border-top:1px solid #eef0f2;padding-top:16px;">
                            The link opens your bank's authorization page and brings you back to BankSync when you are done.
                            The link stops working after a while; if it does, the next BankSync run sends a fresh one.
                            <div style="margin-top:12px;font-size:12px;color:#9aa0a6;word-break:break-all;">{url}</div>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
                """;
    }

    internal static string BuildText(BankConnection connection, string url) =>
        $"""
         BankSync

         Your {connection.BankName} ({connection.Country}) connection needs re-authorization
         before transactions can sync again.

         Authorize here:
         {url}

         The link opens your bank's authorization page and brings you back to BankSync when you
         are done. The link stops working after a while; if it does, the next BankSync run sends
         a fresh one.
         """;
}
