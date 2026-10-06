using ITMartin.Shared.UI.Kolibri;
using ITMartinTilbud.Server;
using ITMartinTilbud.Server.Services;

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
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<FixedDeals>();
const string UserAgent = "ITMartinTilbud/1.0 (ITMartin@Mensa.dk)";
builder.Services.AddHttpClient<TjekOffers>(c => { c.Timeout = TimeSpan.FromSeconds(20); c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent); });
builder.Services.AddHttpClient<AppGap>(c => { c.Timeout = TimeSpan.FromSeconds(30); c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent); });
builder.Services.AddHttpClient<SallingFoodWaste>(c => { c.Timeout = TimeSpan.FromSeconds(20); c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent); });
builder.Services.AddHttpClient<Places>(c => { c.Timeout = TimeSpan.FromSeconds(15); c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent); });

var app = builder.Build();
app.MapKolibri();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/api/tilbud", async (string? q, double? lat, double? lng, int? km, TjekOffers offers, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return Results.BadRequest(new { fejl = "Skriv mindst 2 bogstaver." });
    if (lat is null || lng is null) return Results.BadRequest(new { fejl = "Vælg først hvor du bor." });
    try { return Results.Ok(await offers.SearchAsync(q, lat.Value, lng.Value, km ?? 10, ct)); }
    catch (OffersUnavailableException) { return Results.Json(new { fejl = "Tilbudsavisen svarer ikke lige nu. Prøv igen om lidt." }, statusCode: 503); }
});

// Madspild: Netto/føtex/Bilka markdowns near you (Salling's official API). 404 = no key yet -> the page hides the box.
app.MapGet("/api/madspild", async (double? lat, double? lng, int? km, SallingFoodWaste fw, CancellationToken ct) =>
{
    if (!fw.Enabled) return Results.NotFound(new { fejl = "Madspild er ikke slået til endnu." });
    if (lat is null || lng is null) return Results.BadRequest(new { fejl = "Vælg først hvor du bor." });
    try { return Results.Ok(await fw.NearAsync(lat.Value, lng.Value, km ?? 5, ct)); }
    catch (OffersUnavailableException) { return Results.Json(new { fejl = "Salling svarer ikke lige nu. Prøv igen om lidt." }, statusCode: 503); }
    catch (FoodWasteQuotaException) { return Results.Json(new { fejl = "Madspild er brugt op for i dag (Salling giver 100 opslag om dagen). Prøv igen i morgen." }, statusCode: 429); }
});

// Faste tilbud (weekly deals that are in no leaflet): everyone reads them; only Martin (Tilbud__AdminPin) adds or removes.
var adminPin = app.Configuration["Tilbud:AdminPin"] ?? "";
bool IsAdmin(HttpContext ctx) => adminPin.Length >= 6 && ctx.Request.Headers["X-Pin"] == adminPin;

app.MapGet("/api/faste", (FixedDeals deals) => Results.Ok(deals.All()));
app.MapPost("/api/faste/tjek", (HttpContext ctx) => IsAdmin(ctx) ? Results.Ok() : Results.StatusCode(401));
app.MapPost("/api/faste", (FixedDealRequest r, HttpContext ctx, FixedDeals deals) =>
{
    if (!IsAdmin(ctx)) return Results.StatusCode(401);
    if (string.IsNullOrWhiteSpace(r.Chain) || string.IsNullOrWhiteSpace(r.Item)) return Results.BadRequest(new { fejl = "Skriv både butik og vare." });
    return Results.Ok(deals.Add(r.Chain, r.Item, r.Days ?? [], r.Price, r.Unit, r.NeedsApp, r.Note));
});
app.MapDelete("/api/faste/{id}", (string id, HttpContext ctx, FixedDeals deals) =>
    !IsAdmin(ctx) ? Results.StatusCode(401) : deals.Remove(id) ? Results.Ok() : Results.NotFound());

// App-kløften: per chain this week, how many offers involve the app and what you pay extra without it.
app.MapGet("/api/appgap", async (double? lat, double? lng, AppGap gap, CancellationToken ct) =>
{
    try { return Results.Ok(await gap.ThisWeekAsync(lat ?? 56.16, lng ?? 10.20, ct)); }
    catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException or TaskCanceledException)
    { return Results.Json(new { fejl = "Tilbudsaviserne svarer ikke lige nu. Prøv igen om lidt." }, statusCode: 503); }
});

app.MapGet("/api/sted", async (string? postnr, Places places, CancellationToken ct) =>
    await places.FromPostcodeAsync(postnr ?? "", ct) is { } p ? Results.Ok(p) : Results.NotFound(new { fejl = "Det postnummer kender vi ikke." }));

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly);   // /kolibri/om + /kolibri/hjaelp

app.Run();

public sealed record FixedDealRequest(string Chain, string Item, int[]? Days, decimal? Price, string? Unit, string? NeedsApp, string? Note);
