using ITMartin.Ai;
using ITMartinLibrary.Application;
using ITMartinLibrary.Infrastructure;
using ITMartinLibrary.Infrastructure.Services;
using ITMartinLibrary.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =========================
// CORE SERVICES
// =========================

builder.Services.AddLibraryApplication();
builder.Services.AddLibraryInfrastructure(builder.Configuration);
builder.Services.AddAi();

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
    .AddInteractiveServerComponents();

// =========================
// BUILD
// =========================

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    await db.Database.EnsureCreatedAsync();

    await db.Database.ExecuteSqlRawAsync("DELETE FROM ShelfBooks");
    await db.Database.ExecuteSqlRawAsync("DELETE FROM ScannedShelves");

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
            CONSTRAINT "FK_ShelfBooks_ScannedShelves"
                FOREIGN KEY ("ScannedShelfId") REFERENCES "ScannedShelves" ("Id") ON DELETE CASCADE
        )
        """);
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
