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
        "I Danmark kommer målingerne fra DMI, i Tyskland fra den tyske vejrtjeneste DWD. Andre steder viser appen en beregnet temperatur og skriver det tydeligt.",
    ];
    k.HowTo =
    [
        "Tryk på Find min temperatur, og tillad, at siden må bruge din position.",
        "Det store tal er den nærmeste station. Under det står de andre stationer inden for den afstand, du har valgt.",
        "Vælg en større afstand, hvis der ikke er en station tæt på.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#1d4ed8";
    k.StaticPages = true;
});

builder.Services.AddTemperaturApplication();
builder.Services.AddTemperaturInfrastructure();

var app = builder.Build();
app.MapKolibri();
app.UseStaticFiles();

// The page: the shared Kolibri header and footer around wwwroot/side.html (read once).
var body = File.ReadAllText(Path.Combine(app.Environment.WebRootPath, "side.html"));
app.MapGet("/", (KolibriAppInfo info, HttpContext ctx) =>
{
    ctx.Response.Headers.CacheControl = "no-cache";
    return Results.Content($"""
        <!doctype html><html lang="da" data-family="{info.Family}"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
        <title>Temperatur</title><meta name="description" content="{info.Tagline}"><meta name="theme-color" content="{info.ThemeColor}">
        <link rel="stylesheet" href="/_content/ITMartin.Shared.UI/css/kolibri.css"><link rel="stylesheet" href="/app.css?v=1">
        <script src="/_content/ITMartin.Shared.UI/kolibri.js" defer></script><script src="/app.js?v=1" defer></script>
        <link rel="manifest" href="/manifest.webmanifest"><link rel="apple-touch-icon" href="/{info.IconPath.TrimStart('/')}"></head>
        <body>{KolibriStaticPages.Header(info)}<main class="k-main"><div class="k-content">{body}</div></main>{KolibriStaticPages.Footer(info)}</body></html>
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
        radius = TemperatureRules.ClampRadius(km ?? 25),
    });
});

app.Run();
