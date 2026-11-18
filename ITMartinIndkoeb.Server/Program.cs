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
        "Åbn familiens side fra jeres Snak-rum (knappen Ugens indkøb), eller fra linket.",
        "Har du handlet: skriv beløbet, tryk butikken. Færdig. Eller tag et billede af kvitteringen – så står beløb og butik der, og du trykker bare på butikken.",
        "Øverst står, hvor meget der er tilbage i ugen, og hvad I skal gøre nu – fx \"Lav ugens store indkøb\".",
        "Under Ugemål kan I ændre beløbet, antal ture og grænsen for en supplering.",
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
    var f = await kv.UdfoerAsync(key, s, fil.FileName, ct);
    return f.Fejl is { } fejl ? Results.BadRequest(new { fejl }) : Results.Ok(f);
});
h.MapPost("/koeb", (string key, KoebRequest r, Indkoeb uc) =>
    uc.LaegInd(key, r.Beloeb, r.Butik, r.Hvem, r.Dato) is { } fejl ? Results.BadRequest(new { fejl }) : Results.Ok());
h.MapDelete("/koeb/{id}", (string key, string id, Indkoeb uc) => uc.Slet(key, id) ? Results.Ok() : Results.NotFound());
h.MapPut("/maal", (string key, MaalRequest r, Indkoeb uc) =>
    uc.GemMaal(key, new Maal(r.UgeMaal, r.MaxTure, r.SupplGraense, r.StortFra), r.Navn) is { Count: > 0 } p
        ? Results.BadRequest(new { fejl = string.Join(" ", p) }) : Results.Ok());

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly);
app.Run();

public partial class Program;
