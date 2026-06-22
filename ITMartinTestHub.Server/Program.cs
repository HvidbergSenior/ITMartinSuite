using ITMartinTestHub.Server.Data;
using ITMartinTestHub.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("TestHubDb")
    ?? "Data Source=/app/db/testhub.db";

builder.Services.AddDbContext<TestHubDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddScoped<TestHubService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TestHubDbContext>();
    db.Database.EnsureCreated();
    await SeedService.SeedAppsAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ITMartinTestHub.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
