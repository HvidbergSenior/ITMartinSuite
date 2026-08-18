using System.Globalization;
using ITMartinPoll.Server;
using ITMartinPoll.Server.Components;
using ITMartinPoll.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

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
builder.Services.AddSingleton<ITMartinPoll.Server.Services.DatePollBroadcastService>();

var app = builder.Build();

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
        CREATE TABLE IF NOT EXISTS "DatePolls" (
            "Id"          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "Title"       TEXT    NOT NULL,
            "Description" TEXT    NOT NULL DEFAULT '',
            "ImageName"   TEXT,
            "CreatedAt"   TEXT    NOT NULL,
            "Deadline"    TEXT,
            "IsActive"    INTEGER NOT NULL DEFAULT 1
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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
