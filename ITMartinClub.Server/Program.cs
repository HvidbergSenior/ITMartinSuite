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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClubDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var docsRoot = builder.Configuration["ClubSettings:DocsRoot"] ?? "/app/data/documents";
Directory.CreateDirectory(docsRoot);

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
