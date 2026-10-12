using System.Text.Json;
using ITMartin.Shared.UI.Kolibri;
using ITMartinTjek.Server;
using ITMartinTjek.Application;
using ITMartinTjek.Infrastructure;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Tjek";
    k.KolibriName = "Kolibri Tjek";
    k.Family = "nektar";
    k.Tagline = "Er din enhed klar? Netværk, lyd, kamera og browser – tjekket på et minut, uden at installere noget.";
    k.About =
    [
        "Tjek kører en række små prøver direkte i din browser: hvor hurtigt svarer nettet, virker mikrofonen, kan kameraet åbnes, blokerer noget for videomøder.",
        "Hvert punkt får grøn, gul eller rød – med en forklaring på almindeligt dansk, så du ved om det er noget at gøre ved.",
        "På en Windows-pc kan du desuden køre et lille script, der kigger dybere: drivere, USB-enheder med fejl, lydenheder, ventende genstart. Resultatet vises samme sted.",
        "Gem et tjek under et navn, så du kan sammenligne med 'i går virkede det'.",
    ];
    k.HowTo =
    [
        "Tryk 'Tjek nu' og vent et øjeblik.",
        "Tryk 'Test mikrofon' og sig noget; tryk 'Afspil testtone' og lyt.",
        "Er noget rødt eller gult? Læs hintet – eller skriv hvad der driller og tryk 'Hvad er galt?'.",
        "Windows: hent scriptet under 'Dybt tjek' og kør det – så ser vi drivere og USB-fejl også.",
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 4 * 1024 * 1024);

// DDD/Clean since 2026-10-12: rules in Domain, use cases in Application, SQLite + Claude in Infrastructure.
builder.Services.AddTjekApplication(builder.Configuration.GetValue("Tjek:MaxAiCallsPerDay", 100));
builder.Services.AddTjekInfrastructure(builder.Configuration.GetConnectionString("TjekDb") ?? "Data Source=/app/data/tjek.db");

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Tjek:KeysDir"] ?? "/app/data/keys"))
    .SetApplicationName("tjek");

var app = builder.Build();
app.MapKolibri();

await app.Services.UpgradeTjekDatabaseAsync();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

// .ps1 is not a known MIME type, so the deep script would 404 without this.
var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypes.Mappings[".ps1"] = "text/plain; charset=utf-8";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = contentTypes });

// Probes used by tjek.js: tiny reply for latency, ~2 MB random-ish body for a short download test.
app.MapGet("/api/ping", () => Results.Text("pong"));
var blob = new byte[2 * 1024 * 1024];
Random.Shared.NextBytes(blob);
app.MapGet("/api/blob", (HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    return Results.Bytes(blob, "application/octet-stream");
});

// The deep script (Tjek.ps1) posts its JSON here under a device name; the page picks it up.
app.MapPost("/api/deep", async (HttpContext ctx, Runs runs) =>
{
    using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
    try
    {
        var r = await runs.ReceiveDeepRunAsync(doc.RootElement);
        return Results.Ok(new { ok = r.Ok, warn = r.Warn, bad = r.Bad, message = r.Message });
    }
    catch (ArgumentException ex) { return Results.BadRequest(ex.Message); }
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
