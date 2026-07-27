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
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ClubBroadcastService>();
builder.Services.AddSingleton<ClubPushService>();
builder.Services.AddSingleton<ClubAiService>();
builder.Services.AddSingleton<MatchOcrService>();
builder.Services.AddSingleton<ClubDiscordService>();
builder.Services.AddScoped<ClubAuthService>();
builder.Services.AddScoped<ClubSessionStatusService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClubDbContext>();
    db.Database.EnsureCreated();

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

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "EventRsvps" (
            "Id"           TEXT NOT NULL PRIMARY KEY,
            "EventId"      TEXT NOT NULL,
            "MemberName"   TEXT NOT NULL,
            "Status"       TEXT NOT NULL,
            "RespondedAt"  TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "EventPreps" (
            "Id"             TEXT NOT NULL PRIMARY KEY,
            "EventId"        TEXT NOT NULL,
            "Focus"          TEXT NOT NULL,
            "Checklist"      TEXT NOT NULL,
            "CreatedByName"  TEXT NOT NULL,
            "CreatedAt"      TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "ReadyChecks" (
            "Id"             TEXT NOT NULL PRIMARY KEY,
            "GroupId"        TEXT NOT NULL,
            "CreatedByName"  TEXT NOT NULL,
            "CreatedAt"      TEXT NOT NULL,
            "ExpiresAt"      TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "ReadyCheckResponses" (
            "Id"             TEXT NOT NULL PRIMARY KEY,
            "ReadyCheckId"   TEXT NOT NULL,
            "MemberName"     TEXT NOT NULL,
            "Status"         TEXT NOT NULL,
            "RespondedAt"    TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "SessionNotes" (
            "Id"             TEXT NOT NULL PRIMARY KEY,
            "GroupId"        TEXT NOT NULL,
            "MemberName"     TEXT NOT NULL,
            "Text"           TEXT NOT NULL,
            "CreatedAt"      TEXT NOT NULL,
            "UsedInRecap"    INTEGER NOT NULL DEFAULT 0
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "SessionRecaps" (
            "Id"                TEXT NOT NULL PRIMARY KEY,
            "GroupId"           TEXT NOT NULL,
            "Text"              TEXT NOT NULL,
            "GeneratedByName"   TEXT NOT NULL,
            "CreatedAt"         TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Matches" (
            "Id"             TEXT NOT NULL PRIMARY KEY,
            "GroupId"        TEXT NOT NULL,
            "Label"          TEXT NOT NULL DEFAULT '',
            "BombAttempts"   INTEGER NOT NULL DEFAULT 0,
            "BombSuccesses"  INTEGER NOT NULL DEFAULT 0,
            "CreatedByName"  TEXT NOT NULL DEFAULT '',
            "CreatedAt"      TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "MatchPlayerStats" (
            "Id"            TEXT NOT NULL PRIMARY KEY,
            "MatchId"       TEXT NOT NULL,
            "MemberName"    TEXT NOT NULL,
            "Kills"         INTEGER NOT NULL DEFAULT 0,
            "Deaths"        INTEGER NOT NULL DEFAULT 0,
            "LoneSurvivor"  INTEGER NOT NULL DEFAULT 0
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Opponents" (
            "Id"         TEXT NOT NULL PRIMARY KEY,
            "GroupId"    TEXT NOT NULL,
            "Name"       TEXT NOT NULL,
            "CreatedAt"  TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "OpponentTags" (
            "Id"            TEXT NOT NULL PRIMARY KEY,
            "OpponentId"    TEXT NOT NULL,
            "Preset"        TEXT NOT NULL DEFAULT '',
            "Note"          TEXT NULL,
            "AddedByName"   TEXT NOT NULL DEFAULT '',
            "AddedAt"       TEXT NOT NULL
        )
        """);

    // BulletinPost gained ImageFileName/Tag after initial release - EnsureCreated
    // won't add columns to an existing table, so check+add manually (same lesson
    // as karaoke-web/dreamreader-web earlier tonight).
    var hasTagColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Posts') WHERE name = 'Tag'").AsEnumerable().First() > 0;
    if (!hasTagColumn)
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Posts ADD COLUMN ImageFileName TEXT NULL");
        db.Database.ExecuteSqlRaw("ALTER TABLE Posts ADD COLUMN Tag TEXT NOT NULL DEFAULT 'General'");
    }

    // MemberSession.ExpiresAt / Member.Pin added for session expiry + join-time
    // identity verification - default existing sessions far in the future so
    // this migration doesn't retroactively log anyone out.
    // EF Core names this table "Sessions" (the DbSet property name), not
    // "MemberSessions" (the entity class name) - an earlier version of this
    // migration targeted the wrong name and silently altered a dead,
    // never-queried table while the real one stayed unpatched.
    var hasExpiresAt = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Sessions') WHERE name = 'ExpiresAt'").AsEnumerable().First() > 0;
    if (!hasExpiresAt)
        db.Database.ExecuteSqlRaw("ALTER TABLE Sessions ADD COLUMN ExpiresAt TEXT NOT NULL DEFAULT '9999-12-31 00:00:00'");

    var hasPinColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'Pin'").AsEnumerable().First() > 0;
    if (!hasPinColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN Pin TEXT NOT NULL DEFAULT ''");

    var hasMinutesColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('ReadyChecks') WHERE name = 'Minutes'").AsEnumerable().First() > 0;
    if (!hasMinutesColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE ReadyChecks ADD COLUMN Minutes INTEGER NOT NULL DEFAULT 10");

    var hasPhraseColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('ReadyChecks') WHERE name = 'Phrase'").AsEnumerable().First() > 0;
    if (!hasPhraseColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE ReadyChecks ADD COLUMN Phrase TEXT NULL");

    var hasKindColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('SessionRecaps') WHERE name = 'Kind'").AsEnumerable().First() > 0;
    if (!hasKindColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE SessionRecaps ADD COLUMN Kind TEXT NOT NULL DEFAULT 'Funny'");

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "PlaySessions" (
            "Id"                TEXT NOT NULL PRIMARY KEY,
            "GroupId"           TEXT NOT NULL,
            "Phase"             TEXT NOT NULL DEFAULT 'Invitations',
            "CreatedByName"     TEXT NOT NULL DEFAULT '',
            "CreatedAt"         TEXT NOT NULL,
            "PlayingStartedAt"  TEXT NULL,
            "RecapStartedAt"    TEXT NULL,
            "EndedAt"           TEXT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "LiveUpdates" (
            "Id"              TEXT NOT NULL PRIMARY KEY,
            "PlaySessionId"   TEXT NOT NULL,
            "Text"            TEXT NOT NULL DEFAULT '',
            "CreatedByName"   TEXT NOT NULL DEFAULT '',
            "CreatedAt"       TEXT NOT NULL
        )
        """);

    var hasMediaColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('LiveUpdates') WHERE name = 'MediaFileName'").AsEnumerable().First() > 0;
    if (!hasMediaColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE LiveUpdates ADD COLUMN MediaFileName TEXT NULL");

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "EventTimeSuggestions" (
            "Id"               TEXT NOT NULL PRIMARY KEY,
            "EventId"          TEXT NOT NULL,
            "SuggestedDate"    TEXT NOT NULL,
            "SuggestedByName"  TEXT NOT NULL DEFAULT '',
            "CreatedAt"        TEXT NOT NULL,
            "ExpiresAt"        TEXT NOT NULL DEFAULT '9999-12-31 00:00:00',
            "Resolved"         INTEGER NOT NULL DEFAULT 0
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "EventTimeVotes" (
            "Id"            TEXT NOT NULL PRIMARY KEY,
            "SuggestionId"  TEXT NOT NULL,
            "MemberName"    TEXT NOT NULL DEFAULT '',
            "Status"        TEXT NOT NULL DEFAULT 'Yes'
        )
        """);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var docsRoot = builder.Configuration["ClubSettings:DocsRoot"] ?? "/app/data/documents";
Directory.CreateDirectory(docsRoot);

var photosRoot = builder.Configuration["ClubSettings:PhotosRoot"] ?? "/app/data/photos";
Directory.CreateDirectory(photosRoot);

var liveMediaRoot = builder.Configuration["ClubSettings:LiveMediaRoot"] ?? "/app/data/livemedia";
Directory.CreateDirectory(liveMediaRoot);

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

app.MapGet("/photo/{fileName}", (string fileName) =>
{
    var safeName = Path.GetFileName(fileName);
    var path = Path.Combine(photosRoot, safeName);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/jpeg");
});

// Live-update attachments during Playing phase - unlike /photo this can be a
// short clip too, so it keeps its real extension and serves range-enabled
// (video needs seek support) with a mime picked from that extension.
app.MapGet("/media/{fileName}", (string fileName) =>
{
    var safeName = Path.GetFileName(fileName);
    var path = Path.Combine(liveMediaRoot, safeName);
    if (!File.Exists(path)) return Results.NotFound();
    var mime = Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".mp4" or ".m4v" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        _ => "image/jpeg"
    };
    return Results.File(path, mime, enableRangeProcessing: true);
});

app.MapRazorComponents<ITMartinClub.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();

record ClubPushRequest(Guid GroupId, string MemberName, string Endpoint, string P256DH, string Auth);
