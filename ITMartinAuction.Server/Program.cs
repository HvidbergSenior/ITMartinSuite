using ITMartinAuction.Server.Data;
using ITMartinAuction.Server.Hubs;
using ITMartinAuction.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("AuctionDb")
    ?? "Data Source=/app/db/auction.db";

builder.Services.AddDbContext<AuctionDbContext>(o => o.UseSqlite(dbPath));

builder.Services.AddSignalR();
builder.Services.AddScoped<AuctionService>();
builder.Services.AddHostedService<CleanupService>();
builder.Services.AddSingleton<ToastService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

var photosPath = "/app/data/photos";
Directory.CreateDirectory(photosPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(photosPath),
    RequestPath  = "/photos"
});

app.UseAntiforgery();

app.MapHub<AuctionHub>("/hubs/auction");

app.MapRazorComponents<ITMartinAuction.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
