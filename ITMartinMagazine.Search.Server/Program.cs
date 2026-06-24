using ITMartinMagazine.Search.Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("MagazineDb")
    ?? "Data Source=/app/data/magazine.db";

builder.Services.AddDbContext<MagazineDbContext>(o =>
    o.UseSqlite(dbPath).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

var imagesRoot = builder.Configuration["MagazineSettings:ImagesRoot"] ?? "/app/data/images";

app.MapGet("/covers/{filename}", (string filename) =>
{
    var path = Path.Combine(imagesRoot, filename);
    if (!File.Exists(path)) return Results.NotFound();
    return Results.File(path, "image/jpeg");
});

app.MapRazorComponents<ITMartinMagazine.Search.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
