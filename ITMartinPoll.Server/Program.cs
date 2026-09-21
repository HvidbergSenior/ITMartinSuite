using System.Globalization;
using ITMartinPoll.Server;
using ITMartinPoll.Server.Components;
using ITMartinPoll.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using ITMartin.Shared.UI.Kolibri;

// The container's base image defaults to invariant/en-US, so ToString("dddd d.
// MMMM")-style date formatting (used throughout - deadlines, date polls) came
// out in English day/month names on an otherwise all-Danish app.
var danishCulture = new CultureInfo("da-DK");
CultureInfo.DefaultThreadCurrentCulture = danishCulture;
CultureInfo.DefaultThreadCurrentUICulture = danishCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<PollDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("PollDb")
             ?? "Data Source=/app/data/poll.db"));

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
        "Fire slags: almindelig afstemning, billedvurdering (giv billeder point), datoafstemning (ja/nej/måske pr. dato) og pakker, hvor flere spørgsmål samles til én gruppe.",
    ];
    k.HowTo =
    [
        "Åbn linket, du har fået.",
        "Læs, hvad der spørges om, og sæt dit kryds eller dine point.",
        "Skriv dit navn, hvis der bliver bedt om det, og tryk Stem.",
        "Kom tilbage til samme link for at se resultatet.",
    ];
    k.Version = "2026.09";
    k.ThemeColor = "#f4f6f5";
});

builder.Services.AddSingleton<ITMartinPoll.Server.Services.DatePollBroadcastService>();

var app = builder.Build();
app.MapKolibri();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PollDb>();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Sessions" (
            "Id"        INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "Title"     TEXT    NOT NULL,
            "IsActive"  INTEGER NOT NULL,
            "CreatedAt" TEXT    NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "SessionImages" (
            "Id"        INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "SessionId" INTEGER NOT NULL,
            "FileName"  TEXT    NOT NULL,
            "SortOrder" INTEGER NOT NULL,
            FOREIGN KEY ("SessionId") REFERENCES "Sessions" ("Id") ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS "ImageRatings" (
            "Id"      INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "ImageId" INTEGER NOT NULL,
            "Score"   INTEGER NOT NULL,
            "Comment" TEXT    NOT NULL,
            "RatedAt" TEXT    NOT NULL,
            FOREIGN KEY ("ImageId") REFERENCES "SessionImages" ("Id") ON DELETE CASCADE
        );
    """);
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"Deadline\" TEXT;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"CoverImageName\" TEXT;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"Description\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"ImageRatings\" ADD COLUMN \"VoterToken\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"ImageRatings\" ADD COLUMN \"VoterName\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"Question\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Votes\" ADD COLUMN \"VoterName\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Packages" (
            "Id"        INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "Title"     TEXT    NOT NULL DEFAULT '',
            "Intro"     TEXT    NOT NULL DEFAULT '',
            "IsActive"  INTEGER NOT NULL DEFAULT 1,
            "CreatedAt" TEXT    NOT NULL
        );
    """);
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Packages\" ADD COLUMN \"Instructions\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Packages\" ADD COLUMN \"Audience\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Polls\" ADD COLUMN \"AllowMultiple\" INTEGER NOT NULL DEFAULT 0;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Polls\" ADD COLUMN \"PackageId\" INTEGER NULL;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"Sessions\" ADD COLUMN \"PackageId\" INTEGER NULL;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"SessionImages\" ADD COLUMN \"Caption\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"SessionImages\" ADD COLUMN \"Description\" TEXT NOT NULL DEFAULT '';"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"DatePollResponses\" ADD COLUMN \"IsPreferred\" INTEGER NOT NULL DEFAULT 0;"); }
    catch { /* column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE \"DatePolls\" ADD COLUMN \"Password\" TEXT;"); }
    catch { /* column already exists */ }

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "DatePolls" (
            "Id"          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "Title"       TEXT    NOT NULL,
            "Description" TEXT    NOT NULL DEFAULT '',
            "ImageName"   TEXT,
            "CreatedAt"   TEXT    NOT NULL,
            "Deadline"    TEXT,
            "IsActive"    INTEGER NOT NULL DEFAULT 1,
            "Password"    TEXT
        );
        CREATE TABLE IF NOT EXISTS "DatePollDates" (
            "Id"         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DatePollId" INTEGER NOT NULL,
            "Date"       TEXT    NOT NULL,
            "SortOrder"  INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY ("DatePollId") REFERENCES "DatePolls" ("Id") ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS "DatePollResponses" (
            "Id"          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DateId"      INTEGER NOT NULL,
            "VoterName"   TEXT    NOT NULL DEFAULT '',
            "Status"      TEXT    NOT NULL DEFAULT 'Maybe',
            "Comment"     TEXT    NOT NULL DEFAULT '',
            "RespondedAt" TEXT    NOT NULL,
            "IsPreferred" INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY ("DateId") REFERENCES "DatePollDates" ("Id") ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS "DatePollChat" (
            "Id"         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DatePollId" INTEGER NOT NULL,
            "SenderName" TEXT    NOT NULL DEFAULT '',
            "Text"       TEXT    NOT NULL DEFAULT '',
            "SentAt"     TEXT    NOT NULL,
            FOREIGN KEY ("DatePollId") REFERENCES "DatePolls" ("Id") ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS "DatePollImages" (
            "Id"         INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "DatePollId" INTEGER NOT NULL,
            "FileName"   TEXT    NOT NULL DEFAULT '',
            "SortOrder"  INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY ("DatePollId") REFERENCES "DatePolls" ("Id") ON DELETE CASCADE
        );
    """);

    if (app.Configuration.GetValue<bool>("Poll:SeedDemoData"))
        await ITMartinPoll.Server.Data.DemoSeeder.SeedAsync(db);
}

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
app.UseAntiforgery();

// Paper version of a package - for the neighbours who will not use a link.
// Generated from the live package on every request, so it always matches
// the page. See PackagePdf.
app.MapGet("/pakke/{id:int}/pdf", async (int id, PollDb db) =>
{
    var package = await db.Packages
        .Include(p => p.Polls).ThenInclude(p => p.Options)
        .Include(p => p.Sessions).ThenInclude(s => s.Images)
        .FirstOrDefaultAsync(p => p.Id == id);
    if (package is null) return Results.NotFound();

    var bytes = ITMartinPoll.Server.Services.PackagePdf.Render(package);
    var fileName = string.Concat(package.Title.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-') + ".pdf";
    return Results.File(bytes, "application/pdf", fileName);
});

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ITMartin.Shared.UI.Components.Kolibri.KolibriOm).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
