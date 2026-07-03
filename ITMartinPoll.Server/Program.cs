using ITMartinPoll.Server;
using ITMartinPoll.Server.Components;
using ITMartinPoll.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<PollDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("PollDb")
             ?? "Data Source=/app/data/poll.db"));

builder.Services.AddScoped<AdminSession>();

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
