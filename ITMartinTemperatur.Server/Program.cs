using ITMartin.Shared.UI.Kolibri;
using ITMartinTemperatur.Application;
using ITMartinTemperatur.Domain;
using ITMartinTemperatur.Infrastructure;

// Temperatur (Kolibri Svæv) - 2026-10-15, user: "ONLY temperature right now, right here. maybe within a radius",
// Denmark, Germany, Spain and France. Measured values from national weather stations; the page only calls the API.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Temperatur";
    k.KolibriName = "Kolibri Svæv";
    k.Family = "svaev";
    k.Tagline = "Hvor varmt er det lige her – målt, ikke gættet.";
    k.About =
    [
        "Temperatur viser, hvad termometeret siger på den nærmeste rigtige vejrstation – ikke hvad en vejrudsigt regner med.",
        "I Danmark kommer målingerne fra DMI, i Tyskland fra den tyske vejrtjeneste DWD, i Spanien fra AEMET og i Frankrig fra Météo-France. Andre steder viser appen en beregnet temperatur og skriver det tydeligt.",
    ];
    k.HowTo =
    [
        "Tryk på Find min temperatur, og tillad, at siden må bruge din position.",
        "Det store tal er den nærmeste station. Under det står de andre stationer inden for den afstand, du har valgt.",
        "Vælg en større afstand, hvis der ikke er en station tæt på.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#0b1220";
    k.StaticPages = true;
});

builder.Services.AddTemperaturApplication();
builder.Services.AddTemperaturInfrastructure(builder.Configuration["Temperatur:AemetKey"] ?? "", builder.Configuration["Temperatur:MeteoFranceKey"] ?? "");

var app = builder.Build();
app.MapKolibri();
app.UseStaticFiles();

// The page: the shared Kolibri header and footer around wwwroot/side.html (read once).
var body = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "side.html"));
// A personal greeting for the people this copy is made for (Temperatur__Hilsen, e.g. "Hej Jørgen og Marianne - god tur!").
var greeting = app.Configuration["Temperatur:Hilsen"] ?? "";
// Lys/Mørk like ElPriser and Stem (user 2026-10-22: "temperatur need the same css as the other two apps"); app.js handles the click.
const string ThemeButton = """<button id="themeToggleBtn" class="k-btn k-btn-ghost k-btn-sm" type="button" title="Skift lys/mørk visning">Lys/mørk</button>""";
app.MapGet("/", (KolibriAppInfo info, HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-cache";
    return Results.Content($"""
        <!doctype html><html lang="da" data-family="{info.Family}"><head>
        <script>document.documentElement.setAttribute('data-theme', localStorage.getItem('temperatur_theme') || (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));</script>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
        <title>Temperatur</title><meta name="description" content="{info.Tagline}"><meta name="theme-color" content="{info.ThemeColor}">
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css?v=4"><link rel="stylesheet" href="/app.css?v=4">
        <script src="/_content/ITMartin.Shared.UI/kolibri.js" defer></script><script src="/app.js?v=4" defer></script>
        <link rel="manifest" href="/manifest.webmanifest"><link rel="apple-touch-icon" href="/{info.IconPath.TrimStart('/')}"></head>
        <body>{KolibriStaticPages.Header(info, actions: ThemeButton)}<main class="k-main"><div class="k-content">{(greeting == "" ? "" : $"<p class=\"tp-greeting\">{System.Net.WebUtility.HtmlEncode(greeting)}</p>")}{body}</div></main>{KolibriStaticPages.Footer(info)}</body></html>
        """, "text/html; charset=utf-8");
});

// The temperature here: nearest measured station inside the radius, the others in it, or a calculated value.
app.MapGet("/api/temperatur", async (double lat, double lon, int? km, Temperatures temps, CancellationToken ct) =>
{
    var here = new GeoPoint(lat, lon);
    if (!here.IsValid) return Results.BadRequest();
    var r = await temps.HereAsync(here, km ?? 25, ct);
    object Item(NearbyReading n) => new
    {
        station = n.Reading.Station,
        grader = Math.Round(n.Reading.Celsius, 1),
        km = Math.Round(n.DistanceKm, 1),
        maalt = n.Reading.Observed,
        kilde = n.Reading.Source,
        maaltRigtigt = n.Reading.Measured,
    };
    return Results.Ok(new
    {
        her = r.Main is null ? null : Item(r.Main),
        maalt = r.Measured,
        stationer = r.Nearby.Take(12).Select(Item),
        vejrApps = r.Compare is null ? (double?)null : Math.Round(r.Compare.Celsius, 1),
        forskel = r.Difference,
        radius = TemperatureRules.ClampRadius(km ?? 25),
    });
});

// Find a town by name, for a PC without a position or for "how warm is it in Málaga?".
app.MapGet("/api/steder", async (string navn, IPlaceSearch places, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(navn) || navn.Length > 80) return Results.Ok(Array.Empty<object>());
    var found = await places.FindAsync(navn.Trim(), ct);
    return Results.Ok(found.Select(p => new { navn = p.Label, lat = Math.Round(p.Point.Lat, 4), lon = Math.Round(p.Point.Lon, 4) }));
});

app.Run();
