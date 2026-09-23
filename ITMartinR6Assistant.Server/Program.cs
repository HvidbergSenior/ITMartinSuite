using Microsoft.AspNetCore.DataProtection;
using ITMartinR6Assistant.Application;
using ITMartinR6Assistant.Domain;
using ITMartinR6Assistant.Infrastructure;
using ITMartinR6Assistant.Server;
using ITMartinR6Assistant.Server.Services;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Persist the antiforgery/data-protection keys - otherwise every container
// restart invalidates every open tab ("key not found in the key ring").
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "Data", "keys")))
    .SetApplicationName("r6assistant");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<IR6DataService, R6DataService>();
builder.Services.AddSingleton<SessionStateService>();
builder.Services.AddSingleton<PreGameCheckService>();
builder.Services.AddSingleton<SettingsGuideService>();
builder.Services.AddSingleton<R6RefreshService>();
builder.Services.AddScoped<PlayerIdentityService>();
builder.Services.AddHttpClient();

var app = builder.Build();

// .ps1 isn't a recognized static-file content type by default, so
// UseStaticFiles() silently refuses to serve PreGameCheck.ps1 (ServeUnknownFileTypes
// defaults to false) - add it explicitly rather than blanket-enabling unknown
// file types for the whole wwwroot.
var staticFileTypes = new FileExtensionContentTypeProvider();
staticFileTypes.Mappings[".ps1"] = "text/plain";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticFileTypes });

// Map floor plans live in the mounted data folder (not wwwroot), so they can be refreshed without a rebuild.
var blueprintDir = Path.Combine(AppContext.BaseDirectory, "Data", "blueprints");
Directory.CreateDirectory(blueprintDir);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(blueprintDir),
    RequestPath = "/blueprints"
});
app.UseAntiforgery();

// Hit by the local pre-game PowerShell script (see /pregame page for the
// download) - no auth, matching the rest of this app's no-login pattern, and
// the payload is just local hardware/software state, nothing sensitive.
app.MapPost("/api/pregame/check", async (HttpContext ctx, PreGameCheckService svc, SessionStateService session) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var json = await reader.ReadToEndAsync();
    if (string.IsNullOrWhiteSpace(json)) return Results.BadRequest("Tom payload.");
    var report = await svc.AnalyzeAsync(json);

    // Best-effort: the script includes a "spiller" (player name) field so the
    // team overview page can show everyone's latest submitted setup. A missing
    // or unparsable field just means this submission isn't attributed - the
    // checklist itself is still returned either way.
    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("spiller", out var playerProp) && playerProp.GetString() is { Length: > 0 } player)
        {
            session.SetPlayerSetup(player, new PlayerSetupRecord
            {
                RawJson = json,
                Checklist = report,
                SubmittedAtUtc = DateTimeOffset.UtcNow,
            });
        }
    }
    catch (System.Text.Json.JsonException)
    {
        // Ignore - malformed JSON here would already have failed AnalyzeAsync above.
    }

    // Explicit charset is required here - without it, Windows PowerShell 5.1's
    // Invoke-RestMethod has no way to know this UTF-8 response isn't Latin-1
    // and silently misdecodes every Danish character (ae/oe/aa) into mojibake,
    // even though the bytes on the wire are correct the whole time.
    return Results.Text(report, "text/plain; charset=utf-8");
});

app.MapGet("/api/settings-guide/{setting}", async (string setting, SettingsGuideService svc) =>
{
    var explanation = await svc.ExplainAsync(Uri.UnescapeDataString(setting));
    return Results.Text(explanation, "text/plain; charset=utf-8");
});

// Same free fetch as the "Hent nyeste data" button, for scripted runs; PIN-protected so it can't be spammed.
app.MapPost("/api/refresh-data", async (HttpContext ctx, IConfiguration config, R6RefreshService svc) =>
{
    var pin = config["R6:UpdatePin"];
    if (string.IsNullOrEmpty(pin) || ctx.Request.Headers["X-Pin"] != pin) return Results.Unauthorized();
    await svc.RefreshAsync(ctx.RequestAborted);
    return Results.Text(svc.Status, "text/plain; charset=utf-8");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
