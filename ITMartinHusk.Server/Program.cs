using ITMartin.Shared.UI.Kolibri;
using ITMartinHusk.Application;
using ITMartinHusk.Infrastructure;
using ITMartinHusk.Server;

var builder = WebApplication.CreateBuilder(args);

// Husk (2026-10-30), built on ElPriser as the template ("ElPriser is the ONE to look at always"). The "Hvorfor" is Martin's own
// forløb (user: "use my case as the Why this helper case") - told calmly, help and fix, no fingers pointed.
builder.Services.AddKolibri(k =>
{
    k.Name = "Husk";
    k.KolibriName = "Kolibri Puls";
    k.Family = "puls";
    k.Tagline = "Kun i dag og i morgen – én ting ad gangen.";
    k.About =
    [
        "Husk minder dig om dine aftaler med a-kassen, kommunen og andre – og om ny post. Den er ikke en kalender: den viser kun i dag og i morgen, én ting ad gangen.",
        "Hvorfor Husk? Den 29. oktober 2026 var jeg indkaldt til en jobmesse. Jeg havde bekræftet mødet i Jobnet og havde påmindelser slået til alle steder – men påmindelsen kom ikke, og jeg glemte det. Jeg har ADHD og kan kun overskue i dag og måske i morgen. Dagen efter var jeg meldt udeblevet til a-kassen. Husk er det værktøj, jeg selv manglede. – Martin",
        "Bolde i luften: alt det, du har i gang, står under Bolde – hver ting med ét næste skridt, hvem du venter på og en frist. Forsiden viser kun én bold ad gangen, så du ikke skal holde styr på det hele i hovedet.",
        "Husk er lavet til mennesker med ADHD og autisme – og alle andre, der har mange bolde i luften: samme opbygning hver gang, enkle ord, ingen pop-ups og altid ét klart næste skridt.",
        "Husk peger ikke fingre ad nogen. Systemerne virker hver for sig – Husk samler overblikket et sted.",
        "Husk gemmer kun det, du selv skriver: en titel, et tidspunkt, et sted og hvem det er fra. Aldrig dine breve, dit CPR-nummer eller indholdet af en sag.",
    ];
    k.HowTo =
    [
        "Forsiden viser kun den næste ting. Tryk \"Jeg er klar\", når du er klar, og \"Jeg kom\", når du har været der.",
        "Tilføj et møde: tryk Tilføj, skriv hvad, hvornår og hvor, og tryk Husk det.",
        "Ny post i e-Boks eller Digital Post? Tryk \"Ny post\" på forsiden og skriv, hvem den er fra. Husk minder dig hver halve time, til du trykker Læst.",
        "Slå beskeder til nederst på forsiden. Så får du en besked aftenen før kl. 19, om morgenen kl. 7.30 og en time før.",
        "Nåede du ikke et møde? Husk spørger et kvarter efter. Skriv eller ring samme dag – så mister du kun den ene dag.",
        "Bolde: under forsiden står det næste skridt i én bold. Tryk Gjort og skriv det næste skridt – eller hvem du nu venter på. Tomt begge steder = bolden er færdig. Ikke nu = vis den næste bold.",
        "Se alle bolde under Bolde. De er delt i I gang, Venter på andre, Parkeret og Færdig. Tryk på en bold for at rette den, eller tryk \"Det er det næste\" for at sætte den forrest.",
        "Regler: under Regler står reglerne for dagpenge med almindelige ord – hvad de betyder, hvad du gør, og hvor de kommer fra. Skriv dit eget tal for jobsøgning (fra brevet \"Krav til jobsøgning\"), så minder Husk dig fredag kl. 12 og igen mandag kl. 9, hvis ugen mangler i Joblog.",
        "Tjek-runden kommer én gang om dagen: kig i Jobnet, Mit AKA og Digital Post, et sted ad gangen.",
    ];
    k.Version = "2026.11.06.2";
    k.ThemeColor = "#0b1220";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHuskInfrastructure(new HuskInfraOptions(builder.Configuration["DataDir"] ?? "/data"));
builder.Services.AddHuskApplication();
builder.Services.AddHostedService<ReminderJob>();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");

// Martin's own first dates (2026-10-30) - only when the list has never had anything.
app.Services.GetRequiredService<HuskUseCases>().SeedOnce(
[
    ("Jobsamtale med Job Aarhus", new DateTime(2026, 11, 13, 9, 30, 0), "", "Jobnet"),
    ("Rådighedssamtale i a-kassen", new DateTime(2026, 11, 26, 9, 0, 0), "Mariane Thomsens Gade 2F, 7. sal", "A-kassen"),
]);

app.UseHuskPinGate();
app.UseStaticFiles();
app.UseAntiforgery();

// ── Beskeder (wwwroot/js/push.js) ──
app.MapGet("/api/push/public-key", (HuskUseCases husk) => Results.Text(husk.PublicKey));
app.MapPost("/api/push/subscribe", (PushSignup req, HuskUseCases husk) => { husk.Subscribe(req); return Results.Ok(); }).DisableAntiforgery();
app.MapPost("/api/push/unsubscribe", (EndpointRequest req, HuskUseCases husk) => { husk.Unsubscribe(req.Endpoint); return Results.Ok(); }).DisableAntiforgery();
app.MapGet("/api/push/status", (string endpoint, HuskUseCases husk) => husk.IsSubscribed(endpoint) ? Results.Ok() : Results.NotFound());
app.MapPost("/api/push/test", async (EndpointRequest req, HuskUseCases husk) => await husk.TestAsync(req.Endpoint) ? Results.Ok() : Results.NotFound())
    .DisableAntiforgery();

// ── Bolde i luften: the list as JSON, and many balls made/updated at once (matched on key). PIN cookie or X-Husk-Key. ──
var apiJson = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
    { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
app.MapGet("/api/bolde", (BallUseCases balls) => Results.Json(balls.All(), apiJson));
app.MapPost("/api/bolde", (List<BallInput> req, BallUseCases balls) => Results.Json(balls.Save(req))).DisableAntiforgery();

app.MapKolibri();
app.MapRazorComponents<ITMartinHusk.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public sealed record EndpointRequest(string Endpoint);

/// <summary>Every minute: the reminders that are due go to every phone that said yes.</summary>
public sealed class ReminderJob(HuskUseCases husk, ILogger<ReminderJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try { await husk.TickAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Husk tick failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
