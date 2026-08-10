using ITMartinFamily.Infrastructure;
using ITMartinFamily.Server.Components;
using ITMartinFamily.Server.Hubs;
using ITMartinFamily.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/data/keys"));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o =>
    {
        o.MaximumReceiveMessageSize = 5 * 1024 * 1024;
        o.KeepAliveInterval = TimeSpan.FromSeconds(15);
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    });

builder.Services.AddSignalR();
builder.Services.AddFamilyInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ToastService>();
builder.Services.AddHostedService<ITMartinFamily.Server.Services.TaskReminderService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FamilyDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Add columns / tables introduced after initial schema (safe to re-run)
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Tasks ADD COLUMN FamilyId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'"); } catch { }
    // Existing tasks predate the backlog feature - they were already "today"
    // tasks under the old behavior, so default them to not-backlog (0).
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Tasks ADD COLUMN IsBacklog INTEGER NOT NULL DEFAULT 0"); } catch { }
    // Flat tasks-overview model: a task is either unassigned, assigned to
    // someone (from creation or via claim), or completed - no more daily
    // backlog/rotation. Old IsBacklog/Date/ClaimedAt/ImagePath columns are
    // left in place unused rather than migrated.
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Tasks ADD COLUMN AssignedTo TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Tasks ADD COLUMN CompletedBy TEXT"); } catch { }
    // Existing rows used ClaimedBy for the same purpose - carry it forward.
    try { db.Database.ExecuteSqlRaw("UPDATE Tasks SET AssignedTo = ClaimedBy WHERE AssignedTo IS NULL AND ClaimedBy IS NOT NULL"); } catch { }
    try { db.Database.ExecuteSqlRaw("UPDATE Tasks SET CompletedBy = ClaimedBy WHERE CompletedBy IS NULL AND CompletedAt IS NOT NULL"); } catch { }

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Families (
            Id       TEXT NOT NULL PRIMARY KEY,
            Slug     TEXT NOT NULL,
            Name     TEXT NOT NULL,
            Password TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Members (
            Id        TEXT NOT NULL PRIMARY KEY,
            FamilyId  TEXT NOT NULL,
            Name      TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Sessions (
            Id        TEXT NOT NULL PRIMARY KEY,
            MemberId  TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Chat (
            Id         TEXT NOT NULL PRIMARY KEY,
            FamilyId   TEXT NOT NULL,
            SenderName TEXT NOT NULL,
            Text       TEXT NOT NULL,
            SentAt     TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS PushSubscriptions (
            Id         TEXT NOT NULL PRIMARY KEY,
            FamilyId   TEXT NOT NULL,
            MemberName TEXT NOT NULL,
            Endpoint   TEXT NOT NULL,
            P256DH     TEXT NOT NULL,
            Auth       TEXT NOT NULL,
            CreatedAt  TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS Reminders (
            Id               TEXT NOT NULL PRIMARY KEY,
            FamilyId         TEXT NOT NULL,
            MemberName       TEXT NOT NULL,
            Text             TEXT NOT NULL,
            Date             TEXT NOT NULL,
            Done             INTEGER NOT NULL DEFAULT 0,
            CreatedAt        TEXT NOT NULL,
            RemindAt         TEXT,
            NotificationSent INTEGER NOT NULL DEFAULT 0
        )
        """);
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Reminders ADD COLUMN RemindAt TEXT"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Reminders ADD COLUMN NotificationSent INTEGER NOT NULL DEFAULT 0"); } catch { }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Reminders ADD COLUMN PhotoPath TEXT"); } catch { }

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS StoredItems (
            Id        TEXT NOT NULL PRIMARY KEY,
            FamilyId  TEXT NOT NULL,
            Name      TEXT NOT NULL,
            Location  TEXT NOT NULL,
            Notes     TEXT,
            PhotoPath TEXT,
            StoredAt  TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL
        )
        """);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "data", "tasks"));
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "data", "findit-photos"));
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "data", "reminders"));

app.UseStaticFiles();
app.UseAntiforgery();

app.MapHub<FamilyHub>("/hubs/family");

app.MapPost("/api/push/subscribe", async (PushSubscribeRequest req, ITMartinFamily.Application.Interfaces.IPushSubscriptionRepository repo) =>
{
    await repo.UpsertAsync(new ITMartinFamily.Domain.Entities.PushSubscription
    {
        FamilyId   = req.FamilyId,
        MemberName = req.MemberName,
        Endpoint   = req.Endpoint,
        P256DH     = req.P256DH,
        Auth       = req.Auth
    });
    return Results.Ok();
});

app.MapGet("/findit-photo/{id:guid}", async (Guid id, ITMartinFamily.Application.Interfaces.IFamilyStoredItemRepository repo) =>
{
    var item = await repo.GetByIdAsync(id);
    if (item?.PhotoPath is null || !File.Exists(item.PhotoPath)) return Results.NotFound();
    return Results.File(await File.ReadAllBytesAsync(item.PhotoPath), "image/jpeg");
});

app.MapGet("/reminder-photo/{id:guid}", async (Guid id, ITMartinFamily.Infrastructure.FamilyDbContext db) =>
{
    var r = await db.Reminders.FindAsync(id);
    if (r?.PhotoPath is null || !File.Exists(r.PhotoPath)) return Results.NotFound();
    return Results.File(await File.ReadAllBytesAsync(r.PhotoPath), "image/jpeg");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

record PushSubscribeRequest(Guid FamilyId, string MemberName, string Endpoint, string P256DH, string Auth);
