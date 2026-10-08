using System.Text.Json;
using ITMartin.Shared.UI.Kolibri;
using ITMartinTjek.Server;
using ITMartinTjek.Server.Data;
using ITMartinTjek.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

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

var dbPath = builder.Configuration.GetConnectionString("TjekDb") ?? "Data Source=/app/data/tjek.db";
builder.Services.AddDbContextFactory<TjekDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<AiBudget>();
builder.Services.AddSingleton<TjekAi>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Tjek:KeysDir"] ?? "/app/data/keys"))
    .SetApplicationName("tjek");

var app = builder.Build();
app.MapKolibri();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TjekDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    // EnsureCreated does not add new columns to an existing file.
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Snapshots\" ADD COLUMN \"OwnerKey\" TEXT NOT NULL DEFAULT '';"); } catch { /* already there */ }
}

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
app.MapPost("/api/deep", async (HttpContext ctx, IDbContextFactory<TjekDbContext> factory) =>
{
    using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
    var root = doc.RootElement;
    var device = root.TryGetProperty("device", out var d) ? d.GetString() ?? "" : "";
    if (string.IsNullOrWhiteSpace(device)) return Results.BadRequest("device mangler");
    // The code shown on the tjek page in the visitor's own browser - without it nobody could find the result again,
    // and with it nobody else can see it.
    var key = root.TryGetProperty("key", out var k) ? (k.GetString() ?? "").Trim().ToUpperInvariant() : "";
    if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Z0-9]{8,40}$")) return Results.BadRequest("Koden fra tjek-siden mangler. Hent scriptet igen.");
    var checks = root.TryGetProperty("checks", out var c) ? c : default;
    int ok = 0, warn = 0, bad = 0;
    if (checks.ValueKind == JsonValueKind.Array)
        foreach (var x in checks.EnumerateArray())
        {
            var s = x.TryGetProperty("status", out var st) ? st.GetString() : "";
            if (s == "ok") ok++; else if (s == "warn") warn++; else if (s == "bad") bad++;
        }
    await using var db = await factory.CreateDbContextAsync();
    db.Snapshots.Add(new Snapshot
    {
        Device = device.Trim(), OwnerKey = key, Kind = "deep", Json = root.GetRawText(), Ok = ok, Warn = warn, Bad = bad,
        Summary = $"{ok} ok, {warn} gule, {bad} røde",
    });
    await db.SaveChangesAsync();
    return Results.Ok(new { ok, warn, bad, message = $"Modtaget: {ok} ok, {warn} gule, {bad} røde. Åbn https://tjek.itmartin.dk og vælg enheden '{device}'." });
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
