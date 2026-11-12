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
        // Short and plain (user 2026-11-12: "The formål description need to be shorter and more understandable"); details live in HowTo.
        "Lavet af en med ADHD – til dig med ADHD.",
        "Husk minder dig om dine aftaler – med a-kassen, kommunen og andre. Kun i dag og i morgen, én ting ad gangen.",
        "Hvorfor? Jeg glemte selv en jobmesse, fordi påmindelsen aldrig kom. Husk er det værktøj, jeg selv manglede. – Martin",
        "Kun du kan se din liste, og Husk gemmer kun det, du selv skriver – aldrig breve eller CPR-nummer.",
    ];
    k.HowTo =
    [
        "Gratis og Min Husk: med dit kundenummer får du gratis I dag (i dag og i morgen, én ting ad gangen), Tilføj og Ny post. Min Husk giver også Bolde i luften og Alt – 5 kr. om ugen inkl. moms, kun mens den er tændt. Intet abonnement: slukket koster 0 kr. Skriv til Martin, så slår han det til.",
        "Log ind: skriv dit kundenummer (8 cifre – det står i mailen fra Martin) og tryk Åbn min Husk. Telefonen husker dig bagefter. Log ud øverst i menuen.",
        "Se et eksempel: tryk Se et eksempel på login-siden. Så ser du en Husk med opdigtede ting og kan prøve alle knapperne. Tryk Luk eksemplet, når du er færdig.",
        "Forsiden viser kun den næste ting. Tryk \"Jeg er klar\", når du er klar, og \"Jeg kom\", når du har været der.",
        "Tilføj et møde: tryk Tilføj, skriv hvad, hvornår og hvor, og tryk Husk det.",
        "Ny post i e-Boks eller Digital Post? Tryk \"Ny post\" på forsiden og skriv, hvem den er fra. Husk minder dig hver halve time, til du trykker Læst.",
        "Slå beskeder til nederst på forsiden. Så får du en besked aftenen før kl. 19, om morgenen kl. 7.30 og en time før. Bolde med en frist minder dig aftenen før kl. 19 og om morgenen på dagen kl. 7.30.",
        "Nåede du ikke et møde? Husk spørger et kvarter efter. Skriv eller ring samme dag – så mister du kun den ene dag.",
        "Bolde: under forsiden står det næste skridt i én bold. Tryk Gjort og skriv det næste skridt – eller hvem du nu venter på. Tomt begge steder = bolden er færdig. Ikke nu = vis den næste bold.",
        "Kommer du fra Todoist? Under Bolde → Hent fra Todoist vælger du den CSV-fil, Todoist laver (projektet → ⋯ → Eksportér som skabelon → CSV). Hver opgave bliver en bold, en sektion bliver et område, og den første delopgave bliver det næste skridt. Bolde, du har i forvejen, bliver ikke rørt.",
        "Se alle bolde under Bolde. De er delt i I gang, Venter på andre, Parkeret og Færdig. Tryk på en bold for at rette den, eller tryk \"Det er det næste\" for at sætte den forrest.",
        "Job: under Job står alle de job, du har kigget på – Set, Søgt og Fravalgt – og hvor mange du har søgt i denne uge og de sidste 4 uger (heraf opslåede og deltid), som a-kassen tæller. Tryk Jeg har søgt og Skrevet i Joblog. Husk siger til, hvis du allerede har søgt hos samme sted.",
        "Regler: under Job → Se reglerne står reglerne for dagpenge med almindelige ord – hvad de betyder, hvad du gør, og hvor de kommer fra. Skriv dit eget tal for jobsøgning (fra brevet \"Krav til jobsøgning\"), så minder Husk dig fredag kl. 12 og igen mandag kl. 9, hvis ugen mangler i Joblog.",
        "Tjek-runden kommer én gang om dagen: kig i Jobnet, Mit AKA og Digital Post, et sted ad gangen.",
    ];
    k.Version = "2026.11.12.3";
    k.ThemeColor = "#0b1220";
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHuskInfrastructure(new HuskInfraOptions(builder.Configuration["DataDir"] ?? "/data"), builder.Configuration);
builder.Services.AddHuskApplication();
builder.Services.AddHostedService<ReminderJob>();

var app = builder.Build();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");

// Martin's own first dates (2026-10-30) - only when the list has never had anything.
app.Services.GetRequiredService<HuskSpaces>().For(HuskPeople.Martin).Husk.SeedOnce(
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

// ── Bolde i luften: the list as JSON, and many balls made/updated at once (matched on key). Martin only: PIN cookie or X-Husk-Key. ──
var apiJson = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
    { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
app.MapGet("/api/bolde", (BallUseCases balls) => Results.Json(balls.All(), apiJson));
app.MapPost("/api/bolde", (List<BallInput> req, BallUseCases balls) => Results.Json(balls.Save(req))).DisableAntiforgery();

// ── Mine job: the same key and gate as /api/bolde (PinGate opens /api/bolde* and /api/job* with X-Husk-Key). ──
app.MapGet("/api/job", (JobUseCases jobs) => Results.Json(jobs.All(), apiJson));
app.MapPost("/api/job", (List<JobInput> req, JobUseCases jobs) => Results.Json(jobs.Save(req))).DisableAntiforgery();

app.MapKolibri();
app.MapRazorComponents<ITMartinHusk.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public sealed record EndpointRequest(string Endpoint);

/// <summary>Every minute: each person's reminders that are due go to that person's own phones.</summary>
public sealed class ReminderJob(HuskSpaces husk, ILogger<ReminderJob> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try { await husk.TickAllAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { log.LogWarning(ex, "Husk tick failed"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
