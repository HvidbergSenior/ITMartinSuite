using ITMartin.Shared.UI.Kolibri;
using ITMartinForloebet.Server;
using ITMartinForloebet.Application;
using ITMartinForloebet.Domain;
using ITMartinForloebet.Infrastructure;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Forløbet";
    k.KolibriName = "Kolibri Baglæns";
    k.Family = "baglaens";
    k.Tagline = "Hvad gælder, hvad behøver du ikke, og hvad gør du nu.";
    k.About =
    [
        "Forløbet er til, når du står i en sag mod et system: en afgift, et afslag, en afgørelse, en klage. Brevene er lange, fristerne korte, og det meste af teksten er støj.",
        "Hvert brev bliver kogt ned til tre bokse: Hvad gælder, Hvad behøver du ikke, og Gør nu – med dato. Sagen ligger som en tidslinje, så du altid ved, hvor du er.",
        "Når sagen lukkes, skriver du, hvad du lærte – så næste gang går hurtigere.",
    ];
    k.HowTo =
    [
        "Læg det seneste brev ind (foto eller tekst).",
        "Læs de tre bokse – og gør det, der står under Gør nu.",
        "Kom tilbage, når der kommer et nyt brev.",
        "Skriv, hvorfor du går hele vejen – og hvad du tror, der sker, mens sagen stadig er åben.",
        "Har du en historie med samme modpart, så tryk \"Har du også en historie med …?\" – så lægges den sammen med de andre.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
    k.OwnOmPage = true; // Pages/Om.razor is the editorial statement - keep it
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 32 * 1024 * 1024);

// DDD/Clean since 2026-10-12: rules on the Forloeb entity (Domain), use cases in Application.Sager,
// SQLite + document files + Claude in Infrastructure.
var cfg = builder.Configuration;
builder.Services.AddForloebetApplication(
    new ForloebLimits(
        cfg.GetValue("Forloebet:MaxFileBytes", 8L * 1024 * 1024),
        cfg.GetValue("Forloebet:MaxDocsPerForloeb", 20),
        cfg.GetValue("Forloebet:MaxTrinPerForloeb", 40)),
    cfg.GetValue("Forloebet:MaxAiCallsPerDay", 300));
builder.Services.AddForloebetInfrastructure(
    cfg.GetConnectionString("ForloebetDb") ?? "Data Source=/app/data/forloebet.db",
    cfg["Forloebet:DocsDir"] ?? "/app/data/docs",
    Path.Combine(builder.Environment.ContentRootPath, "Knowledge"));

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Forloebet:KeysDir"] ?? "/app/data/keys"))
    .SetApplicationName("forloebet");

var app = builder.Build();
app.MapKolibri();

await app.Services.UpgradeForloebetDatabaseAsync();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

// Owner-only evidence: streamed only when the request carries the forløb's edit key.
app.MapGet("/dok/{slug}/{id:int}", async (string slug, int id, string? k, Sager sager) =>
{
    var file = await sager.DokumentFilAsync(slug, id, k);
    return file is { } f && File.Exists(f.Path) ? Results.File(f.Path, f.Dokument.ContentType, f.Dokument.FileName) : Results.NotFound();
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
