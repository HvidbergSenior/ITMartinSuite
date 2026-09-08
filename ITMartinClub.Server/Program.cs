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
builder.Services.AddScoped<ClubAuthService>();
builder.Services.AddScoped<AssignmentTaskService>();
builder.Services.AddSingleton<AdminPinRateLimiterService>();

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

    var hasContactInfoColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'ContactInfo'").AsEnumerable().First() > 0;
    if (!hasContactInfoColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN ContactInfo TEXT NULL");

    var hasRoleColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'Role'").AsEnumerable().First() > 0;
    if (!hasRoleColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN Role TEXT NULL");

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

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "MainTasks" (
            "Id"                TEXT NOT NULL PRIMARY KEY,
            "GroupId"           TEXT NOT NULL,
            "Title"             TEXT NOT NULL,
            "DefinitionOfDone"  TEXT NULL,
            "SortOrder"         INTEGER NOT NULL DEFAULT 0,
            "CreatedAt"         TEXT NOT NULL
        )
        """);

    // Assignment gained multi-assignee support - AssignedToNames (semicolon
    // list) replaces the old single AssignedToName column. Backfill from the
    // old column (still present in the DB, just unmapped now) then leave it
    // alone, matching how other columns here get retired in place.
    var hasAssignedToNamesColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Assignments') WHERE name = 'AssignedToNames'").AsEnumerable().First() > 0;
    if (!hasAssignedToNamesColumn)
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Assignments ADD COLUMN AssignedToNames TEXT NOT NULL DEFAULT ''");
        db.Database.ExecuteSqlRaw("UPDATE Assignments SET AssignedToNames = AssignedToName WHERE AssignedToName IS NOT NULL AND AssignedToName <> ''");
    }

    var hasMainTaskIdColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Assignments') WHERE name = 'MainTaskId'").AsEnumerable().First() > 0;
    if (!hasMainTaskIdColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Assignments ADD COLUMN MainTaskId TEXT NULL");

    var hasIsDailyColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('MainTasks') WHERE name = 'IsDaily'").AsEnumerable().First() > 0;
    if (!hasIsDailyColumn)
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE MainTasks ADD COLUMN IsDaily INTEGER NOT NULL DEFAULT 0");

        // Familien Hvidberg's board is a daily chore checklist, not a one-off
        // backlog like Bogshoppen's - flip its existing main tasks on once,
        // right when the column is created, so it doesn't fight later toggles.
        db.Database.ExecuteSqlRaw("""
            UPDATE MainTasks SET IsDaily = 1
            WHERE GroupId = (SELECT Id FROM Groups WHERE Slug = 'hvidberg')
            """);
    }

    var hasRecurrenceDayColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('MainTasks') WHERE name = 'RecurrenceDayOfWeek'").AsEnumerable().First() > 0;
    if (!hasRecurrenceDayColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE MainTasks ADD COLUMN RecurrenceDayOfWeek INTEGER NULL");

    // Bogshoppen is the pilot group for the task-board-first front page - once
    // a group has any MainTasks, GroupHome switches from the general dashboard
    // to showing only the open-task board grouped by these. Seed once; leave
    // alone afterwards so renames/reordering in the app stick.
    var bogshoppenGroup = db.Groups.FirstOrDefault(g => g.Slug == "bogshoppen");
    if (bogshoppenGroup is not null && !db.MainTasks.Any(m => m.GroupId == bogshoppenGroup.Id))
    {
        var seedTitles = new[] { "Salg", "Organisering", "Opbevaring", "Transport", "Andet" };
        for (var i = 0; i < seedTitles.Length; i++)
            db.MainTasks.Add(new ITMartinClub.Server.Data.Entities.MainTask { GroupId = bogshoppenGroup.Id, Title = seedTitles[i], SortOrder = i });
        db.SaveChanges();
    }

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "FoundItems" (
            "Id"            TEXT NOT NULL PRIMARY KEY,
            "GroupId"       TEXT NOT NULL,
            "Name"          TEXT NOT NULL,
            "Location"      TEXT NOT NULL,
            "Notes"         TEXT NULL,
            "PhotoFileName" TEXT NULL,
            "StoredByName"  TEXT NOT NULL DEFAULT '',
            "StoredAt"      TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "PersonalReminders" (
            "Id"            TEXT NOT NULL PRIMARY KEY,
            "GroupId"       TEXT NOT NULL,
            "MemberName"    TEXT NOT NULL DEFAULT '',
            "Text"          TEXT NOT NULL,
            "Date"          TEXT NOT NULL,
            "Done"          INTEGER NOT NULL DEFAULT 0,
            "PhotoFileName" TEXT NULL,
            "CreatedAt"     TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "StorageLocations" (
            "Id"             TEXT NOT NULL PRIMARY KEY,
            "GroupId"        TEXT NOT NULL,
            "Name"           TEXT NOT NULL,
            "Address"        TEXT NULL,
            "ApproxSize"     TEXT NULL,
            "CreatedByName"  TEXT NOT NULL DEFAULT '',
            "CreatedAt"      TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Vehicles" (
            "Id"             TEXT NOT NULL PRIMARY KEY,
            "GroupId"        TEXT NOT NULL,
            "Name"           TEXT NOT NULL,
            "Availability"   TEXT NULL,
            "CreatedByName"  TEXT NOT NULL DEFAULT '',
            "CreatedAt"      TEXT NOT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "Ideas" (
            "Id"              TEXT NOT NULL PRIMARY KEY,
            "GroupId"         TEXT NOT NULL,
            "Title"           TEXT NOT NULL,
            "Description"     TEXT NULL,
            "ProposedByName"  TEXT NOT NULL DEFAULT '',
            "CreatedAt"       TEXT NOT NULL
        )
        """);

    var hasContentsColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('StorageLocations') WHERE name = 'Contents'").AsEnumerable().First() > 0;
    if (!hasContentsColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE StorageLocations ADD COLUMN Contents TEXT NULL");

    var hasStorageLocationIdColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Assignments') WHERE name = 'StorageLocationId'").AsEnumerable().First() > 0;
    if (!hasStorageLocationIdColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Assignments ADD COLUMN StorageLocationId TEXT NULL");

    var hasScheduledForColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Assignments') WHERE name = 'ScheduledFor'").AsEnumerable().First() > 0;
    if (!hasScheduledForColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Assignments ADD COLUMN ScheduledFor TEXT NULL");

    // Demo tier only — set on the demo compose service, never on the real
    // club-web pointed at production data. Idempotent (see DemoSeeder).
    if (app.Configuration.GetValue<bool>("Club:SeedDemoData"))
        await DemoSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

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
