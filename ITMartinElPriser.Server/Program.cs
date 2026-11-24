using ITMartin.Shared.UI.Kolibri;
using ITMartinElPriser.Application;
using ITMartinElPriser.Domain;
using ITMartinElPriser.Infrastructure;
using ITMartinElPriser.Server;
using ITMartinElPriser.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "ElPriser";
    k.KolibriName = "Kolibri Puls";
    k.Family = "puls";
    k.Tagline = "Hvad koster strømmen lige nu – og hvornår er den billig?";
    k.About =
    [
        // Short and plain (user 2026-11-12: "The formål description need to be shorter and more understandable"); details live in HowTo.
        "Se, hvornår strømmen er billigst i dag og i morgen – så du kan vaske, tage opvask og lade bilen i de billige timer.",
        "Det er gratis og kræver ingen konto. Vil du have en besked, før de billige timer starter, så skriv dig op gratis.",
        "Kort og adresser: © OpenStreetMap-bidragydere. Netselskab: Green Power Denmark. Nettariffer og elpriser: Kilde: Energinet.",
    ];
    k.HowTo =
    [
        "Åbn appen fra ikonet på telefonen – forsiden viser prisen nu.",
        "Vælg dit hjem og dit elselskab øverst i boksen med prisen (\"Elselskab … skift\"), så er prisen din egen.",
        "Vil du have besked, når strømmen bliver billig? Skriv dig op gratis på itmartin.dk (tryk Skriv mig op – gratis på forsiden), så får du et nummer på mail. Skriv det under Besked, og tryk Slå beskeder til.",
        "Fik du beskeder, før nummeret kom? De fortsætter. Skriv dig op gratis, og skriv dit nummer under Besked.",
        "Vil du have din elregning forklaret? Det er en del af MinElpris – spørg Martin på itmartin.dk.",
    ];
    k.Version = "2026.11.5";
    k.IconPath = "icon.svg";
    k.ThemeColor = "#0b1220";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
// Energinet / Green Power Denmark / elpris.dk behind the Application ports (ITMartinElPriser.Infrastructure). Singletons with
// their own HttpClient: they cache (prices 30 min, suppliers a day) - per-request instances would throw the cache away.
builder.Services.AddElPriserSources();
// The free app's own adapters (who wants a message, Web Push, PDF text) and its use cases. Settings live in a cookie on
// the visitor's phone (PrefsService = the IHouseholdPrefs port, Server side because it needs the request and JS).
builder.Services.AddElPriserApp(builder.Configuration);
builder.Services.AddElPriserApplication();
builder.Services.AddScoped<PrefsService>();
builder.Services.AddScoped<IHouseholdPrefs>(sp => sp.GetRequiredService<PrefsService>());
builder.Services.AddHostedService<NotificationJob>();

var app = builder.Build();
// Start loading every grid company's tariff (Energinet, ~1 min) now, not on the first visitor's click.
app.Services.GetRequiredService<GridTariffs>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

// ── Push subscription API (called from wwwroot/js/push.js) ──────────────────

app.MapGet("/api/push/public-key", (PushUseCases push) => Results.Text(push.PublicKey));

// Beskeder need the kundenummer (2026-10-21); 400 { error } tells the person why.
app.MapPost("/api/push/subscribe", async (PushSignup req, HttpContext http, PushUseCases push, CancellationToken ct) =>
    await push.SubscribeAsync(req, PrefsService.ReadCookie(http), ct) is ({ } id, _) ? Results.Ok(new { id }) : Results.BadRequest(new { error = "kode" }))
    .DisableAntiforgery();

app.MapPost("/api/push/unsubscribe", (UnsubscribeRequest req, PushUseCases push) => { push.Unsubscribe(req.Endpoint); return Results.Ok(); })
    .DisableAntiforgery();

app.MapGet("/api/push/status", (string endpoint, PushUseCases push) =>
    push.Status(endpoint) is { } s ? Results.Ok(new { s.Name, s.NotifyCheapest, s.NotifyExpensive, HasCode = s.CustomerCode.Length > 0 }) : Results.NotFound());

// "Send mig en test" - proves the whole chain on this phone right now.
app.MapPost("/api/push/test", async (UnsubscribeRequest req, PushUseCases push) =>
    await push.TestAsync(req.Endpoint) switch { null => Results.NotFound(), true => Results.Ok(), false => Results.StatusCode(410) })
    .DisableAntiforgery();

// Plain JSON for anyone who wants the numbers: ?area=DK2&allIn=false (itmartin.dk's "Vidste du" box reads it).
app.MapGet("/api/snapshot", async (GetPriceSnapshot snapshot, HttpContext http, string? area, bool? allIn, CancellationToken ct) =>
    Results.Ok(await snapshot.ExecuteAsync(PrefsService.ReadCookie(http), area, allIn, null, ct)));

app.MapKolibri();

app.MapRazorComponents<ITMartinElPriser.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public sealed record UnsubscribeRequest(string Endpoint);

// For the API tests (WebApplicationFactory<Program>).
public partial class Program;
