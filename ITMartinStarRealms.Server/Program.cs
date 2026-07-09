using ITMartinStarRealms.Server.Data;
using ITMartinStarRealms.Server.Hubs;
using ITMartinStarRealms.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("StarRealmsDb")
    ?? "Data Source=/app/db/starrealms.db";

builder.Services.AddDbContext<StarRealmsDbContext>(o => o.UseSqlite(dbPath));

builder.Services.AddSignalR();
builder.Services.AddScoped<GameService>();
builder.Services.AddSingleton<StarRealmsAiService>();
builder.Services.AddHostedService<CleanupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StarRealmsDbContext>();
    db.Database.EnsureCreated();
    await RulesetSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapHub<GameHub>("/hubs/game");

app.MapRazorComponents<ITMartinStarRealms.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
