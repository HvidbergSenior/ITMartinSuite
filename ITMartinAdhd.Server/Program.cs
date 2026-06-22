using ITMartinAdhd.Infrastructure;
using ITMartinAdhd.Infrastructure.Persistence;
using ITMartinAdhd.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =========================
// RAZOR
// =========================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 5 * 1024 * 1024);

builder.Services.AddSingleton<ToastService>();
builder.Services.AddHostedService<ItemCleanupService>();

// =========================
// DATABASE
// =========================
var connectionString =
    builder.Environment.IsDevelopment()
        ? builder.Configuration.GetConnectionString("AdhdDb")
        : "Data Source=/app/data/adhd.db";

Console.WriteLine($"DB: {connectionString}");

builder.Services.AddAdhdInfrastructure(
    builder.Configuration,
    connectionString ?? "Data Source=adhd.db");

// =========================
// LOGGING
// =========================
builder.Logging.AddFilter(
    "Microsoft.EntityFrameworkCore",
    LogLevel.Warning);

// =========================
// BUILD
// =========================
var app = builder.Build();

// =========================
// DATABASE INIT
// =========================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<AdhdDbContext>();

    db.Database.Migrate();
}

// =========================
// MIDDLEWARE
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

var photoDir = app.Configuration["AdhdSettings:PhotoDir"] ?? "/app/data/photos";
Directory.CreateDirectory(photoDir);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(photoDir),
    RequestPath = "/photos"
});

app.UseAntiforgery();

// =========================
// BLAZOR
// =========================
app.MapRazorComponents<ITMartinAdhd.Server.App>()
    .AddInteractiveServerRenderMode();

// =========================
// RUN
// =========================
app.Run();
