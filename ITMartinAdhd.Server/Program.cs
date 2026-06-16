using ITMartinAdhd.Infrastructure;
using ITMartinAdhd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =========================
// RAZOR
// =========================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
