using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.Pipelines.FaceIndex;
using ITMartinPolstrer.Server;
using ITMartinPolstrer.Server.Data;
using ITMartinPolstrer.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 200 * 1024 * 1024);

var dbPath = builder.Configuration.GetConnectionString("PolstrerDb")
    ?? "Data Source=/app/data/polstrer.db";

builder.Services.AddDbContext<PolstrerDbContext>(o => o.UseSqlite(dbPath));

// Only INasDeliveryService is needed from FileSorter's DI - registered
// directly rather than via AddFileSorterCore(), which would also pull in
// the whole media-processing/workflow stack (its own DbContext, workflow
// executors, etc.) this app has no use for.
builder.Services.AddScoped<INasDeliveryService, NasDeliveryService>();
builder.Services.AddScoped<NasPhotoService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PolstrerDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
