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

    var hasContactInfoColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'ContactInfo'").AsEnumerable().First() > 0;
    if (!hasContactInfoColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN ContactInfo TEXT NULL");

    var hasRoleColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'Role'").AsEnumerable().First() > 0;
    if (!hasRoleColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN Role TEXT NULL");

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

    // Familien Hvidberg: the time-slot booking system for vehicles/bathroom
    // (BookableResource/ResourceBooking, retired) moved back to two ordinary
    // main tasks - people sign up for a subtask under "Køretøj"/"Bad" the
    // same way they claim any other chore, instead of picking a start/end
    // time. Seed once; leave alone afterwards so renames/reordering stick
    // (same pattern as the Bogshoppen seed above).
    var hvidbergGroup = db.Groups.FirstOrDefault(g => g.Slug == "hvidberg");
    if (hvidbergGroup is not null)
    {
        var nextSort = db.MainTasks.Where(m => m.GroupId == hvidbergGroup.Id)
            .Select(m => (int?)m.SortOrder).Max() ?? -1;

        if (!db.MainTasks.Any(m => m.GroupId == hvidbergGroup.Id && m.Title == "Køretøj"))
            db.MainTasks.Add(new ITMartinClub.Server.Data.Entities.MainTask
                { GroupId = hvidbergGroup.Id, Title = "Køretøj", SortOrder = ++nextSort });

        if (!db.MainTasks.Any(m => m.GroupId == hvidbergGroup.Id && m.Title == "Bad"))
            db.MainTasks.Add(new ITMartinClub.Server.Data.Entities.MainTask
                { GroupId = hvidbergGroup.Id, Title = "Bad", SortOrder = ++nextSort });

        db.SaveChanges();
    }

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
