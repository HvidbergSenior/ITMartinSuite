using System.Globalization;
using ITMartinPoll.Server;
using ITMartinPoll.Server.Components;
using Microsoft.Extensions.FileProviders;
using ITMartin.Shared.UI.Kolibri;
using ITMartinPoll.Application;
using ITMartinPoll.Domain;
using ITMartinPoll.Infrastructure;

// The container's base image defaults to invariant/en-US, so ToString("dddd d.
// MMMM")-style date formatting (used throughout - deadlines, date polls) came
// out in English day/month names on an otherwise all-Danish app.
var danishCulture = new CultureInfo("da-DK");
CultureInfo.DefaultThreadCurrentCulture = danishCulture;
CultureInfo.DefaultThreadCurrentUICulture = danishCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var pollDb = builder.Configuration.GetConnectionString("PollDb") ?? "Data Source=/app/data/poll.db";
// The layered part of Stem (2026-10-12): voting, used by the poll page and by /api/polls (bogshoppen.dk). Same poll.db.
builder.Services.AddPollApplication();
builder.Services.AddPollInfrastructure(pollDb, builder.Configuration["Kunder:Url"], builder.Configuration["Kunder:ApiKey"]);

builder.Services.AddScoped<AdminSession>();

// Kolibri.UI identity: names, /om text, /hjaelp contact, /health, PWA manifest.
builder.Services.AddKolibri(k =>
{
    k.Name = "Stem";
    k.KolibriName = "Kolibri Bestøver";
    k.Family = "bestoever";
    k.Tagline = "Beslut noget sammen – uden gruppechat.";
    k.About =
    [
        "Stem er til, når flere skal beslutte noget: en dato, en farve, hvilket foto der skal på væggen, hvem der tager hvad.",
        "Den, der spørger, laver en afstemning på et minut og deler ét link. Alle stemmer fra telefonen – ingen konto, ingen app at hente. Resultatet ligger der bagefter, så ingen behøver spørge igen.",
        "At lave din egen afstemning (Lav ny øverst) er gratis for kunder hos ITMartin og Bogshoppen: du skal bruge dit kundenummer, som du får, når du skriver dig op gratis på itmartin.dk. Den gratis udgave har én åben afstemning ad gangen, og den lukker efter højst en måned; mere er den udvidede udgave.",
        "Fire slags: almindelig afstemning, billedvurdering (giv billeder point), datoafstemning (ja/nej/måske pr. dato) og pakker, hvor flere spørgsmål samles til én gruppe.",
    ];
    k.HowTo =
    [
        "Åbn linket, du har fået.",
        "Læs, hvad der spørges om, og sæt dit kryds eller dine point.",
        "Skriv dit navn, hvis der bliver bedt om det, og tryk Stem.",
        "Kom tilbage til samme link for at se resultatet.",
        "Vil du selv spørge? Tryk Lav ny, skriv dit kundenummer, spørgsmålet og svarene, og send linket videre.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

// Live refresh of the open date-poll and help pages; the use cases raise it through IChangeSignal.
builder.Services.AddSingleton<ITMartinPoll.Server.Services.DatePollBroadcastService>();
builder.Services.AddSingleton<IChangeSignal>(sp => sp.GetRequiredService<ITMartinPoll.Server.Services.DatePollBroadcastService>());

var app = builder.Build();
app.MapKolibri();

await app.Services.UpgradePollDatabaseAsync(app.Configuration.GetValue<bool>("Poll:SeedDemoData"));

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

var imagesPath = Path.Combine("/app/data/images");
if (!Directory.Exists(imagesPath)) Directory.CreateDirectory(imagesPath);

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider    = new PhysicalFileProvider(imagesPath),
    RequestPath     = "/poll-images"
});
// Voting from other sites (2026-10-12: bogshoppen.dk shows the live-auction poll itself, server to server over the LAN).
// Same rules as the poll page - both go through Application.Voting. Anyone may vote on a public poll, so no key.
app.MapGet("/api/polls/{id:int}", async (int id, Voting voting) =>
    await voting.ViewAsync(id) is { } view ? Results.Ok(view) : Results.NotFound());

app.MapPost("/api/polls/{id:int}/vote", async (int id, VoteBody body, Voting voting) =>
{
    var (error, view) = await voting.VoteAsync(id, new VoteRequest(body.OptionIds ?? [], body.Name, body.Email, body.Comment, body.AuctionNews));
    return error is null ? Results.Ok(view) : view is null ? Results.NotFound(new { error }) : Results.BadRequest(new { error, poll = view });
}).DisableAntiforgery();

// Kundekartotek from other sites (2026-10-12): "prøv gratis" sign-ups and one-step unsubscribe by e-mail. Since 2026-10-21
// itmartin.dk talks to Kunder itself; these stay for an older Hjem and pass everything on to Kunder.
app.MapPost("/api/customers", async (CustomerBody body, CustomerList customers) =>
{
    var error = await customers.SignUpAsync(body.Email, body.Name, body.Source ?? "", body.ConsentText ?? "", body.Interests ?? "");
    return error is null ? Results.Ok() : Results.BadRequest(new { error });
}).DisableAntiforgery();

app.MapPost("/api/customers/unsubscribe", async (UnsubscribeBody body, CustomerList customers) =>
{
    await customers.UnsubscribeAsync(body.Email);
    return Results.Ok(new { found = true });
}).DisableAntiforgery();

app.UseAntiforgery();

// Paper version of a package - for the neighbours who will not use a link.
// Generated from the live package on every request, so it always matches
// the page. See PackagePdf.
app.MapGet("/pakke/{id:int}/pdf", async (int id, Packages packages, HttpContext ctx, IConfiguration cfg) =>
{
    // admin only (the ladestander forespørgsel) - the cookie is set when the admin PIN is typed on /admin
    if (ctx.Request.Cookies["poll_admin"] != ITMartinPoll.Server.AdminToken.For(cfg["Poll:AdminPin"] ?? "1234")) return Results.NotFound();
    var package = await packages.GetAsync(id);
    if (package is null) return Results.NotFound();

    var bytes = ITMartinPoll.Server.Services.PackagePdf.Render(package);
    var fileName = string.Concat(package.Title.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-') + ".pdf";
    return Results.File(bytes, "application/pdf", fileName);
});

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public sealed record VoteBody(int[]? OptionIds, string? Name, string? Email, string? Comment, bool AuctionNews);
public sealed record CustomerBody(string? Email, string? Name, string? Source, string? ConsentText, string? Interests);
public sealed record UnsubscribeBody(string? Email);
