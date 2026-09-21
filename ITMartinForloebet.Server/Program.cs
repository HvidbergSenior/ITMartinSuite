using ITMartin.Shared.UI.Kolibri;
using ITMartinForloebet.Server;
using ITMartinForloebet.Server.Data;
using ITMartinForloebet.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

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
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#f4f6f5";
    k.OwnOmPage = true; // Pages/Om.razor is the editorial statement - keep it
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 32 * 1024 * 1024);

var dbPath = builder.Configuration.GetConnectionString("ForloebetDb")
    ?? "Data Source=/app/data/forloebet.db";

builder.Services.AddDbContextFactory<ForloebetDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<AiBudget>();
builder.Services.AddSingleton<Knowledge>();
builder.Services.AddSingleton<ForloebetAi>();
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddSingleton<ForloebService>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Forloebet:KeysDir"] ?? "/app/data/keys"))
    .SetApplicationName("forloebet");

var app = builder.Build();
app.MapKolibri();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ForloebetDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    await Seed.EnsureAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

// Owner-only evidence: streamed only when the request carries the forløb's edit key.
app.MapGet("/dok/{slug}/{id:int}", async (string slug, int id, string? k, IDbContextFactory<ForloebetDbContext> factory, DocumentStore docs) =>
{
    if (string.IsNullOrEmpty(k)) return Results.NotFound();
    await using var db = await factory.CreateDbContextAsync();
    var d = await db.Dokumenter.Include(x => x.Forloeb).AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id && x.Forloeb.Slug == slug);
    if (d is null || d.Forloeb.EditKey != k) return Results.NotFound();
    var path = docs.PathFor(d.StoredName);
    return File.Exists(path) ? Results.File(path, d.ContentType, d.FileName) : Results.NotFound();
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
