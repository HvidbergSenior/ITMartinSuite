using ITMartin.Documents;
using ITMartinAeroMedRecord.Server.Data;
using ITMartinAeroMedRecord.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 10 * 1024 * 1024);

var dbPath = builder.Configuration.GetConnectionString("AeroDb")
    ?? "Data Source=/app/data/aeromedrecord.db";

builder.Services.AddDbContext<AeroDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddScoped<AeroAuthService>();
builder.Services.AddSingleton<AeroPushService>();
builder.Services.AddSingleton<AeroBroadcastService>();
builder.Services.AddDocuments();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AeroDbContext>();
    db.Database.EnsureCreated();

    // EnsureCreated only builds the schema for a brand-new database file - it
    // does nothing once the DB already exists, so a column added to an
    // entity after go-live needs its own explicit check+ALTER here (same
    // pattern as Club's ScheduledFor column).
    var hasPersonligInfoColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'PersonligInfo'").AsEnumerable().First() > 0;
    if (!hasPersonligInfoColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN PersonligInfo TEXT NULL");

    var hasEmailColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'Email'").AsEnumerable().First() > 0;
    if (!hasEmailColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN Email TEXT NULL");

    var hasPhoneColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'Phone'").AsEnumerable().First() > 0;
    if (!hasPhoneColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN Phone TEXT NULL");

    var hasAddressColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'Address'").AsEnumerable().First() > 0;
    if (!hasAddressColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN Address TEXT NULL");

    var hasRoleColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'Role'").AsEnumerable().First() > 0;
    if (!hasRoleColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN Role TEXT NULL");

    var hasPhotoColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'PhotoFileName'").AsEnumerable().First() > 0;
    if (!hasPhotoColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN PhotoFileName TEXT NULL");

    var hasInterestsColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'Interests'").AsEnumerable().First() > 0;
    if (!hasInterestsColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN Interests TEXT NULL");

    // Assignment gained multi-assignee support, matching Club - AssignedToNames
    // (semicolon list) replaces the old single AssignedToName column. Backfill
    // from the old column (still present in the DB, just unmapped now).
    var hasAssignedToNamesColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Assignments') WHERE name = 'AssignedToNames'").AsEnumerable().First() > 0;
    if (!hasAssignedToNamesColumn)
    {
        db.Database.ExecuteSqlRaw("ALTER TABLE Assignments ADD COLUMN AssignedToNames TEXT NOT NULL DEFAULT ''");
        db.Database.ExecuteSqlRaw("UPDATE Assignments SET AssignedToNames = AssignedToName WHERE AssignedToName IS NOT NULL AND AssignedToName <> ''");
    }

    // The old AssignedToName column is NOT NULL with no SQL-level default, so
    // once it's unmapped in EF (see AssignedToNames above), every insert that
    // no longer sets it violates that constraint and crashes the circuit.
    // Drop it outright rather than just leaving it unmapped.
    var hasOldAssignedToNameColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Assignments') WHERE name = 'AssignedToName'").AsEnumerable().First() > 0;
    if (hasOldAssignedToNameColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Assignments DROP COLUMN AssignedToName");

    // Same reasoning as above - EnsureCreated does nothing for a brand-new
    // table on an already-existing DB either, so ChatMessages needs its own
    // explicit CREATE TABLE, matching the shape EF would have generated.
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS ChatMessages (
            Id TEXT NOT NULL PRIMARY KEY,
            GroupId TEXT NOT NULL,
            AuthorName TEXT NOT NULL,
            Text TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            ImageFileName TEXT NULL,
            ImageContentType TEXT NULL
        )
        """);

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS PushSubscriptions (
            Id TEXT NOT NULL PRIMARY KEY,
            GroupId TEXT NOT NULL,
            MemberName TEXT NOT NULL,
            Endpoint TEXT NOT NULL,
            P256DH TEXT NOT NULL,
            Auth TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        )
        """);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var docsRoot = builder.Configuration["AeroSettings:DocsRoot"] ?? "/app/data/documents";
Directory.CreateDirectory(docsRoot);

var chatImagesRoot = builder.Configuration["AeroSettings:ChatImagesRoot"] ?? "/app/data/chat-images";
Directory.CreateDirectory(chatImagesRoot);

app.MapGet("/chat-image/{messageId:guid}", async (Guid messageId, AeroDbContext db) =>
{
    var msg = await db.ChatMessages.FindAsync(messageId);
    if (msg?.ImageFileName is null) return Results.NotFound();
    var path = Path.Combine(chatImagesRoot, msg.GroupId.ToString(), msg.ImageFileName);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, msg.ImageContentType ?? "application/octet-stream");
});

var memberPhotosRoot = builder.Configuration["AeroSettings:MemberPhotosRoot"] ?? "/app/data/member-photos";
Directory.CreateDirectory(memberPhotosRoot);

app.MapGet("/member-photo/{memberId:guid}", async (Guid memberId, AeroDbContext db) =>
{
    var member = await db.Members.FindAsync(memberId);
    if (member?.PhotoFileName is null) return Results.NotFound();
    var path = Path.Combine(memberPhotosRoot, member.PhotoFileName);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/jpeg");
});

app.MapPost("/api/push/subscribe", async (AeroPushRequest req, AeroDbContext db, AeroPushService push) =>
{
    await push.UpsertSubscriptionAsync(db, new ITMartinAeroMedRecord.Server.Data.Entities.AeroPushSubscription
    {
        GroupId    = req.GroupId,
        MemberName = req.MemberName,
        Endpoint   = req.Endpoint,
        P256DH     = req.P256DH,
        Auth       = req.Auth
    });
    return Results.Ok();
});

app.MapGet("/api/push/key", (AeroPushService push) => Results.Ok(push.GetPublicKey()));

// The original, immutable file - always served as a plain download, never
// rendered/opened in-browser as the primary reading experience (that's what
// DocumentSections are for).
app.MapGet("/download/{docId:guid}", async (Guid docId, AeroDbContext db) =>
{
    var doc = await db.Documents.FindAsync(docId);
    if (doc is null) return Results.NotFound();
    var path = Path.Combine(docsRoot, doc.GroupId.ToString(), doc.StoredFileName);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "application/octet-stream", doc.OriginalFileName);
});

app.MapRazorComponents<ITMartinAeroMedRecord.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();

record AeroPushRequest(Guid GroupId, string MemberName, string Endpoint, string P256DH, string Auth);
