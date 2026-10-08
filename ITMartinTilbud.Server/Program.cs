using ITMartin.Shared.UI.Kolibri;
using ITMartinTilbud.Server;
using ITMartinTilbud.Application;
using ITMartinTilbud.Domain;
using ITMartinTilbud.Infrastructure;

// Tilbud (Kolibri Nektar) - 2026-10-06, user: "app for users who want to know what is on sale in as many groceries as
// possible". Search one item and see every chain's offer near you, cheapest per kg/litre first; "Mine varer" on the phone
// shows this week's offers for the things you always buy. Free, no ads, no login - "Mine varer" lives in the browser.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Tilbud";
    k.KolibriName = "Kolibri Nektar";
    k.Family = "nektar";
    k.Tagline = "Ugens tilbud i alle supermarkeder nær dig – billigste pr. kilo først.";
    k.About =
    [
        "Tilbud viser ugens tilbud fra Lidl, Netto, REMA 1000, føtex, Bilka, Kvickly, SuperBrugsen, MENY, Løvbjerg, SPAR, 365discount og flere – på én side.",
        "Tilbuddene sorteres efter pris pr. kilo eller liter, så en 3-pak og en enkelt pose kan sammenlignes.",
        "Tilbuddene kommer fra eTilbudsavis (Tjek). Tjek altid prisen i butikken – tilbudsaviser kan have trykfejl og gælde i begrænset antal.",
        "Gratis, uden reklamer og uden login. Dine varer gemmes kun i din egen browser.",
    ];
    k.HowTo =
    [
        "Skriv dit postnummer eller tryk 📍 Brug min placering.",
        "Søg efter en vare, fx kaffe, smør eller havregryn. Den billigste pr. kilo står øverst.",
        "Tryk ⭐ ved en søgning for at gemme den under Mine varer. Så ser du dem alle sammen, hver gang du åbner Tilbud.",
        "Læg Tilbud på telefonen med knappen 📲 – så er den ét tryk væk, når du står i butikken.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddRazorComponents();
// Domain rules + use cases + Tjek/Salling/Nominatim/file adapters (ITMartinTilbud.Infrastructure).
builder.Services.AddTilbud(builder.Configuration);

var app = builder.Build();
app.MapKolibri();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

// Danish messages: a wrong request is 400, an outside service down is 503, Salling's day quota spent is 429.
static IResult Problem(TilbudException e) => e switch
{
    FoodWasteQuotaException => Results.Json(new { fejl = e.Message }, statusCode: 429),
    SourceUnavailableException => Results.Json(new { fejl = e.Message }, statusCode: 503),
    _ => Results.BadRequest(new { fejl = e.Message }),
};

app.MapGet("/api/tilbud", async (string? q, double? lat, double? lng, int? km, SearchOffers search, CancellationToken ct) =>
{
    try { return Results.Ok(await search.ExecuteAsync(q ?? "", lat, lng, km, ct)); }
    catch (TilbudException e) { return Problem(e); }
});

// Madspild: Netto/føtex/Bilka markdowns near you (Salling's official API). 404 = no key yet -> the page hides the box.
app.MapGet("/api/madspild", async (double? lat, double? lng, int? km, NearbyFoodWaste foodWaste, CancellationToken ct) =>
{
    if (!foodWaste.Enabled) return Results.NotFound(new { fejl = "Madspild er ikke slået til endnu." });
    try { return Results.Ok(await foodWaste.ExecuteAsync(lat, lng, km, ct)); }
    catch (TilbudException e) { return Problem(e); }
});

// Faste tilbud (weekly deals that are in no leaflet): everyone reads them; only Martin (Tilbud__AdminPin, 6+ chars)
// adds or removes. No PIN configured = nobody can change them.
var adminPin = app.Configuration["Tilbud:AdminPin"] ?? "";
bool IsAdmin(HttpContext ctx) => adminPin.Length >= 6 && ctx.Request.Headers["X-Pin"] == adminPin;

app.MapGet("/api/faste", (FixedDealsBoard board) => Results.Ok(board.All()));
app.MapPost("/api/faste/tjek", (HttpContext ctx) => IsAdmin(ctx) ? Results.Ok() : Results.StatusCode(401));
app.MapPost("/api/faste", (FixedDealRequest r, HttpContext ctx, FixedDealsBoard board) =>
{
    if (!IsAdmin(ctx)) return Results.StatusCode(401);
    try { return Results.Ok(board.Add(r.Chain, r.Item, r.Days, r.Price, r.Unit, r.NeedsApp, r.Note)); }
    catch (TilbudException e) { return Problem(e); }
});
app.MapDelete("/api/faste/{id}", (string id, HttpContext ctx, FixedDealsBoard board) =>
    !IsAdmin(ctx) ? Results.StatusCode(401) : board.Remove(id) ? Results.Ok() : Results.NotFound());

// App-kløften: per chain this week, how many offers involve the app and what you pay extra without it.
app.MapGet("/api/appgap", async (double? lat, double? lng, ThisWeeksAppGap gap, CancellationToken ct) =>
{
    try { return Results.Ok(await gap.ExecuteAsync(lat ?? 56.16, lng ?? 10.20, ct)); }
    catch (TilbudException e) { return Problem(e); }
});

// Free vs extended: the code form posts here (ExtendedAccess.cs).
app.MapExtendedAccess();

app.MapGet("/api/sted", async (string? postnr, IPlaceLookup places, CancellationToken ct) =>
    await places.FromPostcodeAsync(postnr ?? "", ct) is { } p ? Results.Ok(p) : Results.NotFound(new { fejl = "Det postnummer kender vi ikke." }));

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly);   // /kolibri/om + /kolibri/hjaelp

app.Run();

public sealed record FixedDealRequest(string Chain, string Item, int[]? Days, decimal? Price, string? Unit, string? NeedsApp, string? Note);

// For the API tests (WebApplicationFactory<Program>).
public partial class Program;
