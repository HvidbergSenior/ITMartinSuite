using ITMartin.Ai;
using ITMartinMagazine.Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 20 * 1024 * 1024);

var dbPath = builder.Configuration.GetConnectionString("MagazineDb")
    ?? "Data Source=/app/data/magazine.db";

builder.Services.AddDbContext<MagazineDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddAi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MagazineDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var imagesRoot = builder.Configuration["MagazineSettings:ImagesRoot"] ?? "/app/data/images";
Directory.CreateDirectory(imagesRoot);

app.MapGet("/covers/{filename}", (string filename) =>
{
    var path = Path.Combine(imagesRoot, filename);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/jpeg");
});

app.MapRazorComponents<ITMartinMagazine.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
