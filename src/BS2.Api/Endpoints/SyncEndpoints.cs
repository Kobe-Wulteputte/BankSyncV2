using BS2.Api.Auth;
using BS2.Application.Abstractions;
using BS2.Application.Sync;
using BS2.Domain.Enums;
using BS2.Infrastructure.Import;

namespace BS2.Api.Endpoints;

public static class SyncEndpoints
{
    private const long MaxImportBytes = 10 * 1024 * 1024;

    public static RouteGroupBuilder MapSync(this RouteGroupBuilder api)
    {
        var sync = api.MapGroup("/sync");

        sync.MapPost("/", async (ICurrentUser user, SyncService service, CancellationToken ct) =>
        {
            // Fire-and-forget is wrong here: a cancelled request would abort the run halfway.
            // The service saves a Running row first, so the UI can poll /status while this awaits.
            var run = await service.RunAsync(user.UserId, SyncTrigger.Manual, CancellationToken.None);
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

        return api;
    }
}
