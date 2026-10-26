using ITMartin.Shared.UI.Kolibri;
using ITMartinAuction.Application;
using ITMartinAuction.Infrastructure;
using ITMartinAuction.Server.Hubs;
using ITMartinAuction.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Live Auktion";
    k.KolibriName = "Kolibri Reagere";
    k.Family = "reagere";
    k.Tagline = "Byd fra telefonen – i lokalet eller hjemmefra – og se det højeste bud i samme sekund.";
    k.About =
    [
        "Live Auktion er til foreningens loppemarked, dødsboet eller klubbens støtteauktion. Auktionarius lægger tingene ind med foto og mindstepris; deltagerne byder fra telefonen.",
        "Alle ser det højeste bud med det samme. Efter hammerslaget står listen klar: hvem vandt hvad, og hvad de hver især skal betale.",
    ];
    k.HowTo =
    [
        "Auktionarius: tryk Admin, læg tingene ind med foto og mindstepris.",
        "Deltagere: åbn linket, skriv dit navn, og byd. Før auktionen kan du give et forudbud på mindst startprisen – et nyt forudbud erstatter dit gamle.",
        "Vis Skærm-siden på en stor skærm i lokalet, så alle kan følge med.",
    ];
    k.Version = "2026.10.26";
    k.ThemeColor = "#0f1117";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Layers (2026-10-12 restructure): Domain (auctions + bidding rules) / Application (use cases + ports) /
// Infrastructure (SQLite, photo files) / this Server (pages, SignalR hub, countdown, jobs).
builder.Services.AddAuctionInfrastructure(builder.Configuration);
builder.Services.AddAuctionApplication();
builder.Services.AddSignalR();
builder.Services.AddScoped<IAuctionBroadcast, SignalRBroadcast>();
builder.Services.AddSingleton<CountdownService>();
builder.Services.AddSingleton<ICountdown>(sp => sp.GetRequiredService<CountdownService>());
builder.Services.AddHostedService<CleanupJob>();
builder.Services.AddSingleton<ToastService>();

var app = builder.Build();
await app.Services.UpgradeAuctionDatabaseAsync(app.Configuration);

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

var photosPath = ITMartinAuction.Infrastructure.DependencyInjection.PhotosDir(app.Configuration);
Directory.CreateDirectory(photosPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(photosPath),
    RequestPath  = "/photos"
});

// ── Bogshoppen Lager (2026-10-05) ───────────────────────────────────────
// The register sends items to an auction and fetches the results back (sold / not sold, winning bid, buyer).
// Lager must keep the result itself: CleanupService deletes a session 7 days after it was created.
// Server to server only, with a shared key (Auction__LagerKey = Lager__AuctionKey in magic.env).
var lagerKey = app.Configuration["Auction:LagerKey"] ?? "";
bool LagerKeyOk(HttpContext ctx) =>
    lagerKey.Length >= 16 && ctx.Request.Headers.TryGetValue("X-Lager-Key", out var k) && k == lagerKey;

app.MapGet("/api/lager/sessions", async (HttpContext ctx, LagerUseCases lager) =>
    LagerKeyOk(ctx) ? Results.Ok(await lager.OpenSessionsAsync()) : Results.Unauthorized());

// Items from Lager now carry their mark (Highlight): "🏆 GULD – sælges for ca. 1.500 kr andre steder" and so on.
app.MapPost("/api/lager/items", async (HttpContext ctx, LagerItemIn x, LagerUseCases lager) =>
{
    if (!LagerKeyOk(ctx)) return Results.Unauthorized();
    try { return await lager.AddAsync(x) is { } added ? Results.Ok(new { added.Id, added.Code }) : Results.NotFound("Auktionen findes ikke"); }
    catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
}).DisableAntiforgery();

app.MapPost("/api/lager/status", async (HttpContext ctx, List<Guid> ids, LagerUseCases lager) =>
    LagerKeyOk(ctx) ? Results.Ok(await lager.StatusAsync(ids)) : Results.Unauthorized()).DisableAntiforgery();

app.UseAntiforgery();

app.MapHub<AuctionHub>("/hubs/auction");

app.MapKolibri();

app.MapRazorComponents<ITMartinAuction.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
