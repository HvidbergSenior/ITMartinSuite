using ITMartinForloebet.Server;
using ITMartinForloebet.Server.Data;
using ITMartinForloebet.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 32 * 1024 * 1024);

var dbPath = builder.Configuration.GetConnectionString("ForloebetDb")
    ?? "Data Source=/app/data/forloebet.db";

builder.Services.AddDbContextFactory<ForloebetDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<AiBudget>();
builder.Services.AddSingleton<Knowledge>();
builder.Services.AddSingleton<ForloebetAi>();
builder.Services.AddSingleton<DocumentStore>();
builder.Services.AddSingleton<ForloebService>();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Forloebet:KeysDir"] ?? "/app/data/keys"))
    .SetApplicationName("forloebet");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ForloebetDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
    await Seed.EnsureAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

// Owner-only evidence: streamed only when the request carries the forløb's edit key.
app.MapGet("/dok/{slug}/{id:int}", async (string slug, int id, string? k, IDbContextFactory<ForloebetDbContext> factory, DocumentStore docs) =>
{
    if (string.IsNullOrEmpty(k)) return Results.NotFound();
    await using var db = await factory.CreateDbContextAsync();
    var d = await db.Dokumenter.Include(x => x.Forloeb).AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id && x.Forloeb.Slug == slug);
    if (d is null || d.Forloeb.EditKey != k) return Results.NotFound();
    var path = docs.PathFor(d.StoredName);
    return File.Exists(path) ? Results.File(path, d.ContentType, d.FileName) : Results.NotFound();
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
