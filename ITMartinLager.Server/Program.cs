using ITMartin.Shared.UI.Kolibri;
using ITMartinLager.Server;
using ITMartinLager.Server.Data;
using ITMartinLager.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Bogshoppen Lager (2026-10-05, user: "100000+ products I need to sell ... all trade concentrated on Bogshoppen.dk").
// Step 1 of the plan: the register. Seller of record is ITMartin (has the CVR); Bogshoppen is the shop name.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKolibri(k =>
{
    k.Name = "Bogshoppen Lager";
    k.KolibriName = "Kolibri Rede";
    k.Family = "rede";
    k.Tagline = "Tag et billede af en bunke – så står det hele på lageret.";
    k.About =
    [
        "Bogshoppen Lager er registret over alt det, Bogshoppen sælger: bøger, Anders And-blade, Jumbobøger, tegneserier, magasiner, CD'er, DVD'er, Blu-rays, konsolspil og Magic-kort.",
        "Du lægger 10-20 ting ud side om side og tager ét billede. Så læser AI'en titel, nummer, år og stand på dem alle på én gang, og de lægges i den kasse, du står ved.",
        "Hver kasse har et nummer og en QR-label, så alt kan findes igen med søgningen. Webshop, auktioner og markedspladser bygger senere på det samme register.",
    ];
    k.HowTo =
    [
        "Vælg eller opret kassen, du lægger tingene i, under Registrér.",
        "Læg en bunke ud med forsiden op, og tryk 📷 Tag billede.",
        "Ret det, AI'en har læst forkert (gul = usikker), og tryk Gem.",
        "Find alt igen under Søg – med kassenummer.",
    ];
    k.Version = "2026.10";
    k.ThemeColor = "#f4f6f5";
});

var dataDir = builder.Configuration["Lager:DataDir"] ?? "/app/data";
Directory.CreateDirectory(Path.Combine(dataDir, "photos"));
builder.Services.AddDbContextFactory<LagerDb>(o => o.UseSqlite($"Data Source={Path.Combine(dataDir, "lager.db")}"));
builder.Services.AddSingleton<PileReader>();
builder.Services.AddHttpClient<AuctionLink>(c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHostedService<AuctionSync>();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")))
    .SetApplicationName("bogshoppen-lager");

builder.Services.AddRazorComponents().AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 64 * 1024);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var f = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LagerDb>>();
    using var db = f.CreateDbContext();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
    // EnsureCreated never changes an existing database, so columns added later are added here.
    var conn = db.Database.GetDbConnection();
    conn.Open();
    foreach (var (col, type) in new[]
    {
        ("Interest", "TEXT NOT NULL DEFAULT ''"), ("ItemPhoto", "TEXT NOT NULL DEFAULT ''"),
        ("AuctionItemId", "TEXT NOT NULL DEFAULT ''"), ("AuctionCode", "TEXT NOT NULL DEFAULT ''"),
        ("SoldPrice", "REAL NULL"), ("SoldTo", "TEXT NOT NULL DEFAULT ''"), ("SoldAt", "TEXT NULL"),
    })
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('Items') WHERE name = '{col}'";
        if (Convert.ToInt32(cmd.ExecuteScalar()) == 0)
            db.Database.ExecuteSqlRaw($"ALTER TABLE Items ADD COLUMN {col} {type}");
    }
}

app.MapKolibri();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.UseStaticFiles();

// One shared PIN for Martin and Rico (Lager__Pin in magic.env). Everything but login and static files needs it.
var pin = app.Configuration["Lager:Pin"] ?? "";
bool LoggedIn(HttpContext ctx) => pin.Length >= 6 && ctx.Request.Cookies["lager_auth"] == pin;

app.Use(async (ctx, next) =>
{
    var p = ctx.Request.Path.Value ?? "";
    var open = p.StartsWith("/login") || p.StartsWith("/api/login") || p.StartsWith("/_framework") || p.StartsWith("/_content")
            || p.StartsWith("/kolibri") || p.StartsWith("/om") || p.StartsWith("/hjaelp") || p.StartsWith("/api/hjaelp")
            || p.StartsWith("/health") || p.StartsWith("/manifest.webmanifest") || p.EndsWith(".css") || p.EndsWith(".js")
            || p.EndsWith(".svg") || p.EndsWith(".png");
    if (open || LoggedIn(ctx)) { await next(); return; }
    if (p.StartsWith("/_blazor")) { ctx.Response.StatusCode = 401; return; }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/login", (HttpContext ctx, [FromForm(Name = "pin")] string typed) =>
{
    if (pin.Length < 6 || typed != pin) return Results.Redirect("/login?fejl=1");
    ctx.Response.Cookies.Append("lager_auth", pin, new CookieOptions
    {
        HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365)
    });
    return Results.Redirect("/");
}).DisableAntiforgery();

app.MapGet("/api/logout", (HttpContext ctx) => { ctx.Response.Cookies.Delete("lager_auth"); return Results.Redirect("/login"); });

// The registration photos, so a wrong reading can be checked against the picture.
app.MapGet("/foto/{id:int}", async (int id, IDbContextFactory<LagerDb> dbf) =>
{
    await using var db = await dbf.CreateDbContextAsync();
    var ph = await db.Photos.FindAsync(id);
    var file = ph is null ? null : Path.GetFullPath(Path.Combine(dataDir, ph.Path));
    return file is not null && file.StartsWith(Path.GetFullPath(dataDir)) && File.Exists(file)
        ? Results.File(file, "image/jpeg") : Results.NotFound();
});

app.MapGet("/varefoto/{id:int}", async (int id, IDbContextFactory<LagerDb> dbf) =>
{
    await using var db = await dbf.CreateDbContextAsync();
    var it = await db.Items.FindAsync(id);
    var file = it is null || it.ItemPhoto == "" ? null : Path.GetFullPath(Path.Combine(dataDir, it.ItemPhoto));
    return file is not null && file.StartsWith(Path.GetFullPath(dataDir)) && File.Exists(file)
        ? Results.File(file, "image/jpeg") : Results.NotFound();
});

// Everything as CSV (Excel-friendly: semicolons + BOM) - the way out for accountants, DBA lists and backups.
app.MapGet("/api/eksport.csv", async (IDbContextFactory<LagerDb> dbf) =>
{
    await using var db = await dbf.CreateDbContextAsync();
    var rows = await db.Items.Include(i => i.Box).OrderBy(i => i.Kind).ThenBy(i => i.Series).ThenBy(i => i.Title).ToListAsync();
    static string C(object? v) => "\"" + (v?.ToString() ?? "").Replace("\"", "\"\"") + "\"";
    var sb = new System.Text.StringBuilder("﻿Id;Type;Titel;Kunstner;Serie;Nummer;År;Platform;Stand;Antal;Kasse;Pris;Status;Note;Det interessante\n");
    foreach (var i in rows)
        sb.AppendLine(string.Join(';', i.Id, C(i.Kind), C(i.Title), C(i.Artist), C(i.Series), C(i.Number), i.Year, C(i.Platform),
            C(i.Condition), i.Quantity, C(i.Box?.Code), i.Price, C(i.Status), C(i.Note), C(i.Interest)));
    return Results.File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"bogshoppen-lager-{DateTime.Now:yyyy-MM-dd}.csv");
});

app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program
{
    public static string DataDir(IConfiguration cfg) => cfg["Lager:DataDir"] ?? "/app/data";
}
