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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FamilyDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Add columns / tables introduced after initial schema (safe to re-run)
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Tasks ADD COLUMN FamilyId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'"); } catch { }

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
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "data", "tasks"));

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

app.MapGet("/task-image/{id:guid}", async (Guid id, ITMartinFamily.Application.Interfaces.IDailyTaskRepository repo) =>
{
    var task = await repo.GetByIdAsync(id);
    if (task?.ImagePath is null || !File.Exists(task.ImagePath)) return Results.NotFound();
    return Results.File(await File.ReadAllBytesAsync(task.ImagePath), "image/jpeg");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

record PushSubscribeRequest(Guid FamilyId, string MemberName, string Endpoint, string P256DH, string Auth);
