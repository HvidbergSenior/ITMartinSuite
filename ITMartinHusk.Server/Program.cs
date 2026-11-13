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
    // SHORT (user 2026-11-12 "way to much"): 5 steps, one line each. The rest is shown on the page itself when it is needed.
    k.HowTo =
    [
        "Har du ikke et kundenummer? Skriv dig op gratis på itmartin.dk – så får du det på mail.",
        "Log ind med dit kundenummer. Vil du se først? Tryk Se et eksempel.",
        "Forsiden viser kun den næste ting. Tryk Tilføj for et møde – og Ny post, når der er kommet post.",
        "Slå beskeder til nederst på forsiden. Så minder Husk dig.",
        "Min Husk giver også Bolde i luften og Alt. Prisen aftaler I sammen – skriv til Martin, så slår han den til.",
    ];
    k.Version = "2026.11.12.4";
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
