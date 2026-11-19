using System.Threading.RateLimiting;
using ITMartin.Shared.UI.Kolibri;
using ITMartinIndkoeb.Application;
using ITMartinIndkoeb.Domain;
using ITMartinIndkoeb.Infrastructure;
using ITMartinIndkoeb.Server;
using Microsoft.AspNetCore.DataProtection;

// Ugens indkøb (2026-11-18, user: "a weekly goal... an app that can help the family with this... ONLY groceries", "a snak room",
// "still ddd and template"). The family's page = /h/{key}, opened from the family's Snak room. Everyone sees the same week.
// No login, no bank data, no AI, no ads. Template: EasyPass (static pages + one JS + a small JSON API).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Ugens indkøb";
    k.KolibriName = "Kolibri Nektar";
    k.Family = "nektar";
    k.Tagline = "Familiens dagligvarer for ugen: hvor meget er der tilbage, og hvor mange ture?";
    k.About =
    [
        "Ugens indkøb hjælper familien med at holde dagligvarerne inden for et ugemål – fx 750 kr og 5 ture om ugen.",
        "Alle i familien lægger deres indkøb ind, og alle ser det samme: hvad der er brugt, hvad der er tilbage, og hvad næste skridt er.",
        "Kun dagligvarer. Ingen bankdata, ingen konto, ingen reklamer. Linket er jeres adgang – del det kun med familien.",
    ];
    k.HowTo =
    [
        "Fire trin, og alle i familien kan tage et hvilket som helst af dem.",
        "1 Ugen: se hvor mange kr der er tilbage, og hvad næste skridt er.",
        "2 Find tilbud: søg i ugens tilbud nær jer (postnummer), og tryk Sæt på sedlen.",
        "3 Indkøbsseddel: sedlen er delt op efter butik. Tryk Jeg handler her, så ved de andre, at turen er taget. Sæt flueben i butikken.",
        "4 Har handlet: tag et billede af kvitteringen – så er købet gemt. Eller skriv beløbet, og tryk på butikken. Det, du satte flueben ved, forsvinder fra sedlen.",
        "Under Ugemål og sted kan I ændre beløbet, antal ture, grænsen for en supplering og jeres postnummer.",
    ];
    k.Version = "2026.11.19";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents();
builder.Services.AddIndkoeb(builder.Configuration);
var dataDir = builder.Configuration["Indkoeb:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys"))).SetApplicationName("indkoeb");

static string Visitor(HttpContext ctx) => ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected = async (ctx, ct) => await ctx.HttpContext.Response.WriteAsJsonAsync(new { fejl = "Mange på kort tid. Vent lidt, og prøv igen." }, ct);
    o.AddPolicy("opret", ctx => RateLimitPartition.GetFixedWindowLimiter(Visitor(ctx), _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(24) }));
    o.AddPolicy("tilbud", ctx => RateLimitPartition.GetFixedWindowLimiter(Visitor(ctx), _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromHours(1) }));
    o.AddPolicy("skriv", ctx => RateLimitPartition.GetFixedWindowLimiter(Visitor(ctx), _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromHours(1) }));
});

var app = builder.Build();
app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAntiforgery();

// A new family page. Returns the secret link (the only way in).
app.MapPost("/api/husstand", (OpretRequest r, Indkoeb uc) =>
{
    var h = uc.Opret(r.Navn);
    return Results.Ok(new { noegle = h.Noegle, link = $"/h/{h.Noegle}" });
}).DisableAntiforgery().RequireRateLimiting("opret");

var h = app.MapGroup("/api/h/{key}").DisableAntiforgery().RequireRateLimiting("skriv");
h.MapGet("", (string key, Indkoeb uc, LaesKvittering kv) => uc.Vis(key) is { } o ? Results.Ok(o with { Kvittering = kv.Klar }) : Results.NotFound(new { fejl = "Siden findes ikke. Tjek linket." }));

// "Tag billede af kvitteringen": the photo goes to Kvittering, the answer is a SUGGESTION the family confirms with the shop button.
h.MapPost("/kvittering", async (string key, HttpRequest req, LaesKvittering kv, CancellationToken ct) =>
{
    if (!req.HasFormContentType) return Results.BadRequest(new { fejl = "Vælg et billede." });
    var form = await req.ReadFormAsync(ct);
    var fil = form.Files.FirstOrDefault();
    if (fil is null || fil.Length == 0) return Results.BadRequest(new { fejl = "Vælg et billede." });
    if (fil.Length > 12 * 1024 * 1024) return Results.BadRequest(new { fejl = "Billedet er for stort (højst 12 MB)." });
    await using var s = fil.OpenReadStream();
    var f = await kv.LaesOgGemAsync(key, s, fil.FileName, form["hvem"].FirstOrDefault(), ct);   // a picture is enough: saved at once
    return f.Fejl is { } fejl ? Results.BadRequest(new { fejl }) : Results.Ok(f);
});
h.MapPost("/koeb", (string key, KoebRequest r, Indkoeb uc) =>
    uc.LaegInd(key, r.Beloeb, r.Butik, r.Hvem, r.Dato) is { } fejl ? Results.BadRequest(new { fejl }) : Results.Ok());
h.MapDelete("/koeb/{id}", (string key, string id, Indkoeb uc) => uc.Slet(key, id) ? Results.Ok() : Results.NotFound());
h.MapPut("/maal", (string key, MaalRequest r, Indkoeb uc) =>
    uc.GemMaal(key, new Maal(r.UgeMaal, r.MaxTure, r.SupplGraense, r.StortFra), r.Navn) is { Count: > 0 } p
        ? Results.BadRequest(new { fejl = string.Join(" ", p) }) : Results.Ok());

// Step 2: Find tilbud (via the Tilbud app) + the family's postnummer.
h.MapGet("/tilbud", async (string key, string? q, Indkoeb uc, ITilbudKilde tilbud, CancellationToken ct) =>
    await uc.SoegTilbudAsync(key, q, tilbud, ct) is var svar && svar.Fejl is { } fejl ? Results.BadRequest(new { fejl }) : Results.Ok(svar.Tilbud))
    .RequireRateLimiting("tilbud");
h.MapPut("/sted", async (string key, StedRequest r, Indkoeb uc, ITilbudKilde tilbud, CancellationToken ct) =>
    await uc.SaetStedAsync(key, r.Postnr, tilbud, ct) is { } fejl ? Results.BadRequest(new { fejl }) : Results.Ok());

// Step 3: the shared list + who takes the trip.
h.MapPost("/seddel", (string key, VareRequest r, Indkoeb uc) =>
    uc.TilfoejVare(key, r.Tekst, r.Hvem, r.Butik, r.Tilbud) is { } fejl ? Results.BadRequest(new { fejl }) : Results.Ok());
h.MapPut("/seddel/{id}", (string key, string id, AfkrydsRequest r, Indkoeb uc) => uc.Afkryds(key, id, r.Koebt) ? Results.Ok() : Results.NotFound());
h.MapDelete("/seddel/{id}", (string key, string id, Indkoeb uc) => uc.FjernVare(key, id) ? Results.Ok() : Results.NotFound());
h.MapPut("/tur", (string key, TurRequest r, Indkoeb uc) =>
    uc.TagTur(key, r.Butik, r.Hvem) is { } fejl ? Results.BadRequest(new { fejl }) : Results.Ok());

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly);
app.Run();

public partial class Program;
