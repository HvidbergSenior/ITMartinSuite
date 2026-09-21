using ITMartin.Shared.UI.Kolibri;
using ITMartinAuction.Server.Data;
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

app.UseAntiforgery();

app.MapHub<AuctionHub>("/hubs/auction");

app.MapKolibri();

app.MapRazorComponents<ITMartinAuction.Server.App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
