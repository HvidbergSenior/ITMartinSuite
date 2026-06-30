using ITMartinClub.Server.Data;
using ITMartinClub.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 10 * 1024 * 1024);

var dbPath = builder.Configuration.GetConnectionString("ClubDb")
    ?? "Data Source=/app/data/club.db";

builder.Services.AddDbContext<ClubDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<ClubBroadcastService>();
builder.Services.AddSingleton<ClubPushService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClubDbContext>();
    db.Database.EnsureCreated();

    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "MemberSessions" (
                "Id"        TEXT NOT NULL CONSTRAINT "PK_MemberSessions" PRIMARY KEY,
                "MemberId"  TEXT NOT NULL DEFAULT '',
                "CreatedAt" TEXT NOT NULL DEFAULT '0001-01-01 00:00:00'
            )
            """);
    }
    catch { }

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Chat" (
            "Id"         TEXT NOT NULL PRIMARY KEY,
            "GroupId"    TEXT NOT NULL,
            "MemberId"   TEXT NOT NULL,
            "SenderName" TEXT NOT NULL,
            "Text"       TEXT NOT NULL,
            "SentAt"     TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "PushSubscriptions" (
            "Id"         TEXT NOT NULL PRIMARY KEY,
            "GroupId"    TEXT NOT NULL,
            "MemberName" TEXT NOT NULL,
            "Endpoint"   TEXT NOT NULL,
            "P256DH"     TEXT NOT NULL,
            "Auth"       TEXT NOT NULL,
            "CreatedAt"  TEXT NOT NULL
        )
        """);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var docsRoot = builder.Configuration["ClubSettings:DocsRoot"] ?? "/app/data/documents";
Directory.CreateDirectory(docsRoot);

app.MapPost("/api/push/subscribe", async (ClubPushRequest req, ClubDbContext db, ClubPushService push) =>
{
    await push.UpsertSubscriptionAsync(db, new ITMartinClub.Server.Data.Entities.ClubPushSubscription
    {
        GroupId    = req.GroupId,
        MemberName = req.MemberName,
        Endpoint   = req.Endpoint,
        P256DH     = req.P256DH,
        Auth       = req.Auth
    });
    return Results.Ok();
});

app.MapGet("/api/push/key", (ClubPushService push) => Results.Ok(push.GetPublicKey()));

app.MapGet("/download/{docId:guid}", async (Guid docId, ClubDbContext db) =>
{
    var doc = await db.Documents.FindAsync(docId);
    if (doc is null) return Results.NotFound();
    var path = Path.Combine(docsRoot, doc.GroupId.ToString(), doc.StoredFileName);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "application/octet-stream", doc.OriginalFileName);
});

app.MapRazorComponents<ITMartinClub.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();

record ClubPushRequest(Guid GroupId, string MemberName, string Endpoint, string P256DH, string Auth);
