using ITMartin.Documents;
using ITMartinRedigerDokument.Server.Data;
using ITMartinRedigerDokument.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 10 * 1024 * 1024);

var dbPath = builder.Configuration.GetConnectionString("RedigerDb")
    ?? "Data Source=/app/data/redigerdokument.db";

builder.Services.AddDbContext<RedigerDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddScoped<RedigerAuthService>();
builder.Services.AddDocuments();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RedigerDbContext>();
    db.Database.EnsureCreated();

    // EnsureCreated only builds the schema for a brand-new database file - it
    // does nothing once the DB already exists, so a column added to an
    // entity after go-live needs its own explicit check+ALTER here (same
    // pattern as Club's ScheduledFor column).
    var hasPersonligInfoColumn = db.Database.SqlQueryRaw<int>(
        "SELECT COUNT(*) AS Value FROM pragma_table_info('Members') WHERE name = 'PersonligInfo'").AsEnumerable().First() > 0;
    if (!hasPersonligInfoColumn)
        db.Database.ExecuteSqlRaw("ALTER TABLE Members ADD COLUMN PersonligInfo TEXT NULL");
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var docsRoot = builder.Configuration["RedigerSettings:DocsRoot"] ?? "/app/data/documents";
Directory.CreateDirectory(docsRoot);

// The original, immutable file - always served as a plain download, never
// rendered/opened in-browser as the primary reading experience (that's what
// DocumentSections are for).
app.MapGet("/download/{docId:guid}", async (Guid docId, RedigerDbContext db) =>
{
    var doc = await db.Documents.FindAsync(docId);
    if (doc is null) return Results.NotFound();
    var path = Path.Combine(docsRoot, doc.GroupId.ToString(), doc.StoredFileName);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "application/octet-stream", doc.OriginalFileName);
});

app.MapRazorComponents<ITMartinRedigerDokument.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
