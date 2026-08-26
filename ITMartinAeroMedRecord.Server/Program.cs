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
builder.Services.AddDocuments();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AeroDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var docsRoot = builder.Configuration["AeroSettings:DocsRoot"] ?? "/app/data/documents";
Directory.CreateDirectory(docsRoot);

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
