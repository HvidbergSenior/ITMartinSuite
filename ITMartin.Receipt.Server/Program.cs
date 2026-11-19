using ITMartin.Ai;
using ITMartin.OCR;
using ITMartin.Receipt.Application;
using ITMartin.Receipt.Infrastructure;
using ITMartin.Receipt.Server;
using ITMartin.Receipt.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Without this, keys live in the container's own filesystem and get
// regenerated on every redeploy - any browser tab still open from before
// the redeploy then fails antiforgery validation (a harmless, self-healing
// error in practice, but avoidable) since its cookie was encrypted with
// keys that no longer exist. /app/data is the same volume the receipts DB
// already persists to.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/data/keys"));

builder.Services.AddReceiptApplication();
builder.Services.AddReceiptInfrastructure(builder.Configuration);
builder.Services.AddAi();
builder.Services.AddOcr();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddTransient<ITMartin.Receipt.Application.Services.DiscountCheck>();
builder.Services.AddHttpClient<ITMartin.Receipt.Application.Services.IOfferLookup, ITMartin.Receipt.Infrastructure.TilbudOfferLookup>(c => c.Timeout = TimeSpan.FromSeconds(20));

// =========================
// SIGNALR
// =========================

builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024 * 20;
});

// =========================
// BLAZOR
// =========================

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 5 * 1024 * 1024);

// =========================
// BUILD
// =========================

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReceiptDbContext>();
    await db.Database.EnsureCreatedAsync();
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Transactions\" ADD COLUMN \"IsTemplate\" INTEGER NOT NULL DEFAULT 0"); } catch { }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ReceiptTransactionItem\" ADD COLUMN \"IsSuspicious\" INTEGER NOT NULL DEFAULT 0"); } catch { }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Transactions\" ADD COLUMN \"ImageFileName\" TEXT"); } catch { }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ReceiptTransactionItem\" ADD COLUMN \"RawText\" TEXT"); } catch { }
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Transactions\" ADD COLUMN \"ItemsPhotoFileName\" TEXT"); } catch { }

    if (app.Configuration.GetValue<bool>("Receipt:SeedDemoData"))
        await ITMartin.Receipt.Infrastructure.DemoSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

// =========================
// DATA FOLDERS
// =========================

Directory.CreateDirectory("data");
Directory.CreateDirectory("data/receipts");

app.MapScanApi();   // POST /api/scan for Ugens indkøb (2026-11-19)

// 2026-11-19 (user chose "close them"): the old public pages showed EVERY scanned receipt to anyone and took photos in the
// app. Kvittering is now only the reading service for Ugens indkøb (/api/scan, /api/laes with key); upload happens there.
app.MapGet("/", () => Results.Content("""
    <!doctype html><html lang="da"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
    <title>Kvittering</title><body style="font:17px/1.5 system-ui,sans-serif;max-width:36rem;margin:3rem auto;padding:0 16px">
    <h1>Kvittering</h1><p>Kvitteringer og tilbud lægges nu ind i <b>Ugens indkøb</b>: tag billederne med telefonens kamera, og upload dem dér, når det passer dig.</p>
    <p><a href="https://indkoeb.itmartin.dk/">Gå til Ugens indkøb</a> · <a href="https://itmartin.dk/">ITMartin.dk</a></p></body></html>
    """, "text/html; charset=utf-8"));

app.Run();
