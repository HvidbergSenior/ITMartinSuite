using ITMartinBarTab.Server.Data;
using ITMartinBarTab.Server.Hubs;
using ITMartinBarTab.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("BarTabDb")
    ?? "Data Source=/app/db/bartab.db";

builder.Services.AddDbContext<BarTabDbContext>(o => o.UseSqlite(dbPath));

builder.Services.AddSignalR();

builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<DrinkVisionService>();
builder.Services.AddHostedService<SessionCleanupService>();


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BarTabDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider("/app/data/photos"),
    RequestPath = "/photos"
});

app.UseAntiforgery();

app.MapHub<SessionHub>("/hubs/session");

app.MapRazorComponents<ITMartinBarTab.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
