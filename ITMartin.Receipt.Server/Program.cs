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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
