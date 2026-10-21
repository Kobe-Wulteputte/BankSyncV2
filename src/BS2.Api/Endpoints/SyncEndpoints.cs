using BS2.Api.Auth;
using BS2.Application;
using BS2.Application.Abstractions;
using BS2.Application.Sync;
using BS2.Domain.Enums;
using BS2.Infrastructure.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BS2.Api.Endpoints;

public static class SyncEndpoints
{
    private const long MaxImportBytes = 10 * 1024 * 1024;

    public static RouteGroupBuilder MapSync(this RouteGroupBuilder api)
    {
        var sync = api.MapGroup("/sync");

        sync.MapPost("/", async (DateOnly? from, ICurrentUser user, SyncService service, IClock clock, CancellationToken ct) =>
        {
            if (from > clock.Today) return Results.BadRequest(new { title = "The from date cannot be in the future." });
            // Fire-and-forget is wrong here: a cancelled request would abort the run halfway.
            // The service saves a Running row first, so the UI can poll /status while this awaits.
            var run = await service.RunAsync(user.UserId, SyncTrigger.Manual, CancellationToken.None, from);
            return Results.Accepted("/api/sync/status", run);
        });

        sync.MapGet("/runs", async (int? take, ICurrentUser user, SyncService service, CancellationToken ct) =>
            Results.Ok(await service.GetRunsAsync(user.UserId, Math.Clamp(take ?? 10, 1, 100), ct)));

        sync.MapGet("/status", async (ICurrentUser user, SyncService service, CancellationToken ct) =>
            Results.Ok(await service.GetStatusAsync(user.UserId, ct)));

        api.MapPost("/import/excel", async (IFormFile file, ExcelImportService importer, CancellationToken ct) =>
            {
                if (file.Length == 0) return Results.BadRequest(new { title = "Empty file." });
                // ClosedXML loads the whole workbook into memory; a V1 workbook is well under this.
                if (file.Length > MaxImportBytes) return Results.BadRequest(new { title = "File is larger than 10 MB." });
                await using var stream = file.OpenReadStream();
                return Results.Ok(await importer.ImportAsync(stream, ct));
            })
            .RequireAuthorization(AuthSetup.AdminPolicy)
            .DisableAntiforgery();

        // Sends to the address the expiry mails use, so a success here means those arrive too.
        api.MapPost("/mail/test", async (ICurrentUser user, IAppDbContext db, IOptions<SyncOptions> options, IEmailSender mail, CancellationToken ct) =>
            {
                var userEmail = (await db.Users.Where(u => u.Id == user.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct))?.Trim();
                var recipient = string.IsNullOrEmpty(userEmail) ? options.Value.NotifyEmail : userEmail;
                if (string.IsNullOrWhiteSpace(recipient))
                    return Results.BadRequest(new { title = "No recipient: your account has no email and Sync:NotifyEmail is empty." });
                try
                {
                    await mail.SendAsync(recipient, "BankSync: test mail", "<p>Mail from BankSync works.</p>", "Mail from BankSync works.", ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Admin-only, and the SMTP error is the point of the test.
                    return Results.Problem(title: "Sending failed", detail: ex.Message, statusCode: 502);
                }
                return Results.Ok(new { recipient });
            })
            .RequireAuthorization(AuthSetup.AdminPolicy);

        return api;
    }
}
