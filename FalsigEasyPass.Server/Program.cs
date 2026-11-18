using FalsigEasyPass.Application;
using FalsigEasyPass.Domain;
using FalsigEasyPass.Infrastructure;
using FalsigEasyPass.Server;
using ITMartin.Shared.UI.Kolibri;

// Easy-Pass - our version (2026-11-18). Jacob's Easy-Pass (Base44 + the old tourism-app repo) is the spec, not the code:
// "Mit pas" (your needs, kept in your own browser), find places that fit you, one page per place with what it has,
// "Min tur" (a list you can share), and messages from visitors and places. No account, no ads, no tracking.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Easy-Pass";
    k.KolibriName = "Kolibri Navigere";
    k.Family = "navigere";
    k.Tagline = "Find steder, der passer til dine behov – kørestol, larm, ledsagerkort og meget mere.";
    k.About =
    [
        "Easy-Pass viser, om et sted passer til dig: adgang uden trin, handicaptoilet, stille rum, teleslynge, ledsagerkort og meget mere.",
        "Du sætter kryds i Mit pas én gang. Så står der ved hvert sted: Passer til dig, Tjek selv eller Passer ikke – og hvorfor.",
        "Mit pas og Min tur gemmes kun i din egen browser. Ingen konto, ingen reklamer.",
        "Ikke tjekket betyder, at ingen har tjekket det endnu. Vi lover aldrig noget, ingen har tjekket.",
    ];
    k.HowTo =
    [
        "Svar på fire små spørgsmål: hvad der er svært for dig, hvad der passer på dig, hvor du vil hen, og hvad du har lyst til.",
        "Så får du de steder, der passer til dig, først. Steder hvor ikke alt er tjekket står under Ring og spørg først.",
        "Tryk på et sted. Der står din plan: hvad du skal spørge om, hvad du skal tage med, og en knap der viser dit pas stort ved indgangen.",
        "Bagefter trykker du Det passede eller Noget passede ikke. Det hjælper den næste.",
    ];
    k.Version = "2026.11.18";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents();
builder.Services.AddEasyPass(builder.Configuration);

var app = builder.Build();
app.MapKolibri();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

// --- for everyone ---------------------------------------------------------------------------------------------------
app.MapGet("/api/katalog", () => Results.Ok(new
{
    behov = Katalog.Alle.Select(b => new { b.Noegle, b.Gruppe, b.Tekst }),
    faciliteter = Katalog.Faciliteter,
    kategorier = Katalog.Kategorier,
}));

app.MapGet("/api/steder", (string? q, string? kategori, string? behov, bool? kunPasser, double? lat, double? lng, FindSteder find) =>
    Results.Ok(find.Udfoer(q, kategori, behov, kunPasser == true, lat, lng)));

app.MapGet("/api/steder/{id}", (string id, string? behov, VisSted vis) =>
    vis.Udfoer(id, behov) is { } d ? Results.Ok(d) : Results.NotFound(new { fejl = "Stedet findes ikke." }));

app.MapPost("/api/steder/{id}/besked", (string id, BeskedRequest r, SendTilbagemelding send) =>
{
    var type = r.Godt ? TilbagemeldingType.Godt : r.Ejer ? TilbagemeldingType.Ejer : TilbagemeldingType.Fejl;
    return send.Udfoer(id, type, r.Tekst ?? "", r.Mail) is { } fejl ? Results.BadRequest(new { fejl }) : Results.Ok();
}).DisableAntiforgery();

// --- admin (EasyPass__AdminPin, 6+ chars; none set = nobody can change anything) ----------------------------------------
var adminPin = app.Configuration["EasyPass:AdminPin"] ?? "";
bool IsAdmin(HttpContext ctx) => adminPin.Length >= 6 && ctx.Request.Headers["X-Pin"] == adminPin;

var admin = app.MapGroup("/api/admin").DisableAntiforgery()
    .AddEndpointFilter(async (ctx, next) => IsAdmin(ctx.HttpContext) ? await next(ctx) : Results.StatusCode(401));

admin.MapPost("/tjek", () => Results.Ok());
admin.MapGet("/beskeder", (StedAdmin a, IStedStore steder) => Results.Ok(a.Beskeder().Select(t => new
{
    t.Id, t.StedId, Sted = steder.Hent(t.StedId)?.Navn ?? "(slettet)", Type = t.Type.ToString(), t.Tekst, t.Mail, t.Tid, t.Klaret,
})));
admin.MapPost("/beskeder/{id}/klaret", (string id, StedAdmin a) => { a.Klaret(id); return Results.Ok(); });
admin.MapPost("/sted", (Sted s, StedAdmin a) => a.Gem(s) is { Count: > 0 } p ? Results.BadRequest(new { fejl = string.Join(" ", p) }) : Results.Ok(new { s.Id }));
admin.MapDelete("/sted/{id}", (string id, StedAdmin a) => a.Slet(id) ? Results.Ok() : Results.NotFound());
admin.MapPost("/slet-eksempler", (IStedStore steder, StedAdmin a) =>
{
    var n = steder.Alle().Where(s => s.ErEksempel).Count(s => a.Slet(s.Id));
    return Results.Ok(new { slettet = n });
});
admin.MapPost("/import", async (HttpRequest req, ImportSteder import) =>
{
    using var reader = new StreamReader(req.Body);
    var csv = await reader.ReadToEndAsync();
    if (csv.Length > 20_000_000) return Results.BadRequest(new { fejl = "Filen er for stor (højst 20 MB)." });
    return Results.Ok(import.Udfoer(csv));
});

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly);   // /om + /hjaelp
app.Run();

public partial class Program;
