using ITMartin.Shared.UI.Kolibri;
using ITMartinAuction.Server.Data;
using ITMartinAuction.Server.Data.Entities;
using ITMartinAuction.Server.Hubs;
using ITMartinAuction.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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
        "Deltagere: åbn linket, skriv dit navn, og byd.",
        "Vis Skærm-siden på en stor skærm i lokalet, så alle kan følge med.",
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#0f1117";
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("AuctionDb")
    ?? "Data Source=/app/db/auction.db";

builder.Services.AddDbContext<AuctionDbContext>(o => o.UseSqlite(dbPath));

builder.Services.AddSignalR();
builder.Services.AddScoped<AuctionService>();
builder.Services.AddSingleton<CountdownService>();
builder.Services.AddHostedService<CleanupService>();
builder.Services.AddSingleton<ToastService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();
    db.Database.EnsureCreated();

    // Schema migrations for columns added after initial deploy
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();

    var migrations = new[]
    {
        "ALTER TABLE Sessions ADD COLUMN Status INTEGER NOT NULL DEFAULT 0",
        "ALTER TABLE Sessions ADD COLUMN AdminToken TEXT NOT NULL DEFAULT ''",
        "ALTER TABLE Sessions ADD COLUMN AuctionDate TEXT",
        "ALTER TABLE Items ADD COLUMN LotQuantity INTEGER NOT NULL DEFAULT 1",
        "ALTER TABLE Items ADD COLUMN BuyNowBuyerName TEXT",
        "ALTER TABLE Items ADD COLUMN BuyNowBuyerPhone TEXT",
        "ALTER TABLE Bidders ADD COLUMN Token TEXT NOT NULL DEFAULT ''",
        "ALTER TABLE Bidders ADD COLUMN BidderNumber INTEGER",
        "ALTER TABLE Bidders ADD COLUMN Phone TEXT",
        "ALTER TABLE Bids ADD COLUMN IsPreBid INTEGER NOT NULL DEFAULT 0",
        // ChatMessages table (EnsureCreated handles it for fresh DBs)
        @"CREATE TABLE IF NOT EXISTS ChatMessages (
            Id TEXT NOT NULL PRIMARY KEY,
            SessionId TEXT NOT NULL,
            BidderNumber INTEGER NOT NULL,
            Message TEXT NOT NULL,
            SentAt TEXT NOT NULL
          )",
    };

    foreach (var sql in migrations)
    {
        try
        {
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 &&
            (ex.Message.Contains("duplicate column") || ex.Message.Contains("already exists")))
        {
            // Column or table already exists — ignore
        }
    }

    if (app.Configuration.GetValue<bool>("Auction:SeedDemoData"))
        await ITMartinAuction.Server.Data.DemoSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

var photosPath = "/app/data/photos";
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

app.MapGet("/api/lager/sessions", async (HttpContext ctx, AuctionDbContext db) =>
{
    if (!LagerKeyOk(ctx)) return Results.Unauthorized();
    var sessions = await db.Sessions.Where(s => s.Status != AuctionStatus.Ended)
        .Select(s => new { s.Code, s.Name, Status = s.Status.ToString(), s.AuctionDate, s.CreatedAt, s.ExpiresAt, Items = s.Items.Count })
        .ToListAsync();
    return Results.Ok(sessions.OrderBy(s => s.AuctionDate ?? s.CreatedAt));
});

app.MapPost("/api/lager/items", async (HttpContext ctx, LagerItemIn x, AuctionDbContext db, AuctionService svc) =>
{
    if (!LagerKeyOk(ctx)) return Results.Unauthorized();
    var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.Code == x.Code.ToUpper());
    if (session is null) return Results.NotFound("Auktionen findes ikke");
    if (session.Status == AuctionStatus.Ended) return Results.BadRequest("Auktionen er slut");
    string? photo = null;
    if (!string.IsNullOrEmpty(x.PhotoJpegBase64))
    {
        photo = Path.Combine(photosPath, $"lager-{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(photo, Convert.FromBase64String(x.PhotoJpegBase64));
    }
    var item = await svc.AddItemAsync(session.Code, session.AdminToken, x.Name, x.Description, x.StartingPrice, Math.Max(1, x.LotQuantity), photo);
    return Results.Ok(new { item.Id, session.Code });
}).DisableAntiforgery();

app.MapPost("/api/lager/status", async (HttpContext ctx, List<Guid> ids, AuctionDbContext db) =>
{
    if (!LagerKeyOk(ctx)) return Results.Unauthorized();
    var items = await db.Items.Where(i => ids.Contains(i.Id)).ToListAsync();
    var bidderIds = items.Where(i => i.WinnerBidderId != null).Select(i => i.WinnerBidderId!.Value).ToList();
    var bidders = await db.Bidders.Where(b => bidderIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id);
    return Results.Ok(ids.Select(id =>
    {
        var i = items.FirstOrDefault(x => x.Id == id);
        if (i is null) return new LagerStatusOut(id, "Gone", null, null, null);
        var w = i.WinnerBidderId is { } wid ? bidders.GetValueOrDefault(wid) : null;
        return new LagerStatusOut(id, i.Status.ToString(), i.WinningBid,
            w?.Name ?? i.BuyNowBuyerName, w?.Phone ?? i.BuyNowBuyerPhone);
    }));
}).DisableAntiforgery();

app.UseAntiforgery();

app.MapHub<AuctionHub>("/hubs/auction");

app.MapKolibri();

app.MapRazorComponents<ITMartinAuction.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

record LagerItemIn(string Code, string Name, string? Description, decimal StartingPrice, int LotQuantity, string? PhotoJpegBase64);
record LagerStatusOut(Guid Id, string Status, decimal? WinningBid, string? BuyerName, string? BuyerPhone);
