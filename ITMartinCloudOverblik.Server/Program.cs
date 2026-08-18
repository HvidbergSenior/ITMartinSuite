using ITMartinCloudOverblik.Server.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var dbPath = builder.Configuration.GetConnectionString("CloudDb")
    ?? "Data Source=/app/data/cloud.db";

builder.Services.AddDbContext<CloudDbContext>(o => o.UseSqlite(dbPath));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CloudDbContext>();
    db.Database.EnsureCreated();

    if (app.Configuration.GetValue<bool>("CloudOverblik:SeedDemoData"))
        await ITMartinCloudOverblik.Server.Data.DemoSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ITMartinCloudOverblik.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
