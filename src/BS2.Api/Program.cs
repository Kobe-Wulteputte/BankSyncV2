using System.Text.Json.Serialization;
using BS2.Api.Auth;
using BS2.Api.Endpoints;
using BS2.Api.ErrorHandling;
using BS2.Api.Workers;
using BS2.Application;
using BS2.Application.Abstractions;
using BS2.Application.Banking;
using BS2.Application.Sync;
using BS2.Application.Transactions;
using BS2.Infrastructure;
using BS2.Infrastructure.Classification;
using BS2.Infrastructure.EnableBanking;
using BS2.Infrastructure.Import;
using BS2.Infrastructure.Mail;
using BS2.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Pinned so a Windows Service or container start, which does not run from the app folder,
    // still finds appsettings.json.
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
});

builder.Host.UseWindowsService();
builder.Host.UseSerilog((context, services, cfg) => cfg
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// CreateBuilder already loads user secrets in Development, below appsettings.{Environment}.json.
// Adding them again here would put them on top, the ordering bug V1 fixed in 702d1c9.
builder.Configuration.AddEnvironmentVariables(prefix: "BS2_");

var configuration = builder.Configuration;
var services = builder.Services;

services.Configure<AppOptions>(configuration.GetSection(AppOptions.Section));
services.Configure<SyncOptions>(configuration.GetSection(SyncOptions.Section));

services.AddPersistence(configuration);
services.AddEnableBanking(configuration);
services.AddClassification(configuration);
services.AddMail(configuration);
services.AddExcelImport(configuration);
services.AddBanking();
services.AddSync();
services.AddQueries();

services.AddHttpContextAccessor();
services.AddScoped<ICurrentUser, HttpCurrentUser>();
services.AddAuth0(configuration);

services.AddSingleton<SyncSchedule>();
services.AddSingleton<ISyncSchedule>(sp => sp.GetRequiredService<SyncSchedule>());
services.AddHostedService<SyncWorker>();

services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});
services.AddExceptionHandler<AppExceptionHandler>();
services.AddProblemDetails();
services.AddOpenApi();

var clientOrigin = configuration[$"{AppOptions.Section}:{nameof(AppOptions.ClientOrigin)}"];
if (!string.IsNullOrWhiteSpace(clientOrigin))
{
    // Same-origin requests also carry an Origin header; list our own origin so the CORS middleware
    // does not log a denial for every POST from the served SPA.
    var publicOrigin = configuration[$"{AppOptions.Section}:{nameof(AppOptions.PublicOrigin)}"] ?? "";
    services.AddCors(cors => cors.AddDefaultPolicy(policy => policy
        .WithOrigins(clientOrigin.TrimEnd('/'), publicOrigin.TrimEnd('/'))
        .AllowAnyHeader()
        .AllowAnyMethod()));
}

var app = builder.Build();

// Migrations run at startup: single-instance app, no separate deploy step to forget.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!string.IsNullOrWhiteSpace(clientOrigin)) app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

// Explicit so routing runs AFTER static files. Left implicit, WebApplication puts routing first,
// the SPA fallback endpoint matches every asset URL and StaticFileMiddleware steps aside.
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserProvisioningMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();

app.MapGroup("/api")
    .RequireAuthorization(AuthSetup.AllowListedPolicy)
    .MapMe()
    .MapTransactions()
    .MapAnalytics()
    .MapCatalog()
    .MapBanks()
    .MapSync();

// SPA fallback: anything that is not /api or a static file gets index.html so client-side routes deep-link.
app.MapFallbackToFile("{*path:regex(^(?!api(/|$)).*$)}", "index.html").AllowAnonymous();

app.Run();

public partial class Program;
