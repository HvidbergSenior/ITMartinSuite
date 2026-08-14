using ITMartin.Ai;
using ITMartinLibrary.Application;
using ITMartinLibrary.Domain.Entities;
using ITMartinLibrary.Infrastructure;
using ITMartinLibrary.Infrastructure.Services;
using ITMartinLibrary.Server;
using ITMartinLibrary.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =========================
// CORE SERVICES
// =========================

builder.Services.AddLibraryApplication();
builder.Services.AddLibraryInfrastructure(builder.Configuration);
builder.Services.AddAi();
builder.Services.AddSingleton<ToastService>();

builder.Services.AddHostedService<BarcodeEnrichmentWorker>();

// =========================
// SIGNALR
// =========================

builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024 * 20;
});

// =========================
// BLAZOR
// =========================

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 5 * 1024 * 1024);

// =========================
// BUILD
// =========================

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    await db.Database.EnsureCreatedAsync();

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "ScannedShelves" (
            "Id"          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "ShelfNumber" INTEGER NOT NULL,
            "ImagePath"   TEXT    NOT NULL,
            "ScannedAt"   TEXT    NOT NULL
        )
        """);

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "ShelfBooks" (
            "Id"              INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            "ScannedShelfId"  INTEGER NOT NULL,
            "Title"           TEXT    NOT NULL,
            "Author"          TEXT    NOT NULL,
            "BBoxX"           REAL    NOT NULL,
            "BBoxY"           REAL    NOT NULL,
            "BBoxW"           REAL    NOT NULL,
            "BBoxH"           REAL    NOT NULL,
            "MediaType"       TEXT    NOT NULL DEFAULT 'Book',
            CONSTRAINT "FK_ShelfBooks_ScannedShelves"
                FOREIGN KEY ("ScannedShelfId") REFERENCES "ScannedShelves" ("Id") ON DELETE CASCADE
        )
        """);

    try
    {
        await db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"ShelfBooks\" ADD COLUMN \"MediaType\" TEXT NOT NULL DEFAULT 'Book'");
    }
    catch { }

    // Multi-tenant retrofit - this used to be a single shared collection
    // (Martin's personal books, ~350 already scanned) with no group concept
    // at all. EnsureCreatedAsync() is a no-op here because the DB already
    // exists (it only creates a schema for a brand-new database), so the
    // Groups table needs the same manual bootstrap as ScannedShelves/
    // ShelfBooks did above.
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "Groups" (
            "Id"        TEXT NOT NULL PRIMARY KEY,
            "Slug"      TEXT NOT NULL,
            "Name"      TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL
        )
        """);
    await db.Database.ExecuteSqlRawAsync(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Groups_Slug\" ON \"Groups\" (\"Slug\")");

    // Fixed, well-known id (not Guid.NewGuid()) so the backfill below always
    // targets the same group across every deploy/restart, not a fresh
    // random one each time.
    var personalGroupId = new Guid("00000000-0000-0000-0000-000000000001");

    if (!await db.Groups.AnyAsync(g => g.Slug == "personal"))
        db.Groups.Add(new LibraryGroup { Id = personalGroupId, Slug = "personal", Name = "Martins bogsamling", CreatedAt = DateTime.UtcNow });
    if (!await db.Groups.AnyAsync(g => g.Slug == "bogshoppen"))
        db.Groups.Add(new LibraryGroup { Id = Guid.NewGuid(), Slug = "bogshoppen", Name = "Bogshoppen", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    // table is always one of the three hardcoded literals passed below, never
    // external input - SQL identifiers (table/column names) can't be
    // parameterized in ADO.NET, only values can, so EF1002 below is a false
    // positive here; the actual data value (personalGroupId) does go through
    // a real ADO parameter ({0}), not string interpolation.
#pragma warning disable EF1002
    async Task AddGroupIdColumnAsync(string table)
    {
        var hasColumn = (await db.Database.SqlQueryRaw<int>(
            $"SELECT COUNT(*) AS Value FROM pragma_table_info('{table}') WHERE name = 'GroupId'").ToListAsync()).First() > 0;
        if (hasColumn) return;

        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"GroupId\" TEXT NOT NULL DEFAULT ''");
        // Every row that existed before groups did belonged to the one
        // collection there was - Martin's personal books.
        await db.Database.ExecuteSqlRawAsync(
            $"UPDATE \"{table}\" SET \"GroupId\" = {{0}} WHERE \"GroupId\" = ''",
            personalGroupId.ToString());
    }
#pragma warning restore EF1002

    await AddGroupIdColumnAsync("Items");
    await AddGroupIdColumnAsync("ScannedShelves");
    await AddGroupIdColumnAsync("ShelfBooks");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
