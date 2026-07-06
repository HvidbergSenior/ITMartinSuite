using ITMartinStats.Server.Components;
using ITMartinStats.Server.Data;
using ITMartinStats.Server.Data.Entities;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddDbContext<StatsDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Stats") ?? "Data Source=/app/data/stats.db"));

builder.Services.AddCors(o => o.AddPolicy("track", p =>
    p.WithOrigins(
        "https://www.itmartin.dk",
        "https://itmartin.dk",
        "https://martin.itmartin.dk",
        "http://localhost:5000",
        "http://localhost:5001")
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<StatsDbContext>().Database.EnsureCreated();

app.UseStaticFiles();
app.UseCors("track");
app.UseAntiforgery();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

// Lightweight tracking endpoint — no auth, CORS open to itmartin.dk origins
app.MapPost("/api/hit", async (StatsDbContext db, HttpContext ctx, HitRequest req) =>
{
    var ua = ctx.Request.Headers.UserAgent.ToString();

    // Skip bots
    if (ua.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
        ua.Contains("crawl", StringComparison.OrdinalIgnoreCase) ||
        ua.Contains("spider", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(ua))
        return Results.NoContent();

    var device = ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ? "mobil"
               : ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase) ? "tablet"
               : "desktop";

    var referrer = "";
    if (!string.IsNullOrWhiteSpace(req.Referrer))
    {
        try { referrer = new Uri(req.Referrer).Host; } catch { referrer = req.Referrer[..Math.Min(60, req.Referrer.Length)]; }
    }

    db.Hits.Add(new PageHit
    {
        Path      = req.Path[..Math.Min(200, req.Path.Length)],
        Title     = (req.Title ?? "")[..Math.Min(120, (req.Title ?? "").Length)],
        Referrer  = referrer,
        Device    = device,
        VisitorId = (req.VisitorId ?? "")[..Math.Min(40, (req.VisitorId ?? "").Length)],
    });
    await db.SaveChangesAsync();
    return Results.NoContent();
}).DisableAntiforgery().RequireCors("track");

app.Run();

record HitRequest(string Path, string? Title, string? Referrer, string? VisitorId);
