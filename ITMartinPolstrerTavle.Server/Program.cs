using ITMartinPolstrerTavle.Server;
using ITMartinPolstrer.Core.Data;
using ITMartinPolstrer.Core.Data.Entities;
using ITMartinPolstrer.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 200 * 1024 * 1024);

var dbPath = builder.Configuration.GetConnectionString("PolstrerDb")
    ?? "Data Source=/app/data/polstrer.db";

builder.Services.AddDbContextFactory<PolstrerDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddSingleton<MediaStore>();

// Keys on the data volume, otherwise every deploy invalidates every
// antiforgery cookie already sitting in her browser.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(builder.Configuration["Polstrer:KeysDir"] ?? "/app/data/keys-tavle"))
    .SetApplicationName("polstrer-tavle");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PolstrerDbContext>>();
    using var db = factory.CreateDbContext();
    PolstrerSchema.Ensure(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

// Photos/videos served straight from the data volume.
var media = app.Services.GetRequiredService<MediaStore>();
Directory.CreateDirectory(media.Root);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(media.Root),
    RequestPath = "/media"
});

// Single-PIN login for her; /vis/{slug} (the share page for customers and
// colleagues) and the media behind it are open, everything else is gated.
var adminPin = app.Configuration["Polstrer:AdminPin"] ?? "polstrer2026";

app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";
    var open = path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/vis/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
    if (open || (ctx.Request.Cookies.TryGetValue("polstrer_auth", out var v) && v == adminPin))
    {
        await next();
        return;
    }
    ctx.Response.Redirect("/login");
});

app.MapPost("/api/auth/login", (HttpContext ctx, [FromForm] string pin) =>
{
    if (pin == adminPin)
    {
        ctx.Response.Cookies.Append("polstrer_auth", adminPin, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(365)
        });
        return Results.Redirect("/");
    }
    return Results.Redirect("/login?error=1");
}).DisableAntiforgery();

app.MapGet("/api/auth/logout", (HttpContext ctx) =>
{
    ctx.Response.Cookies.Delete("polstrer_auth");
    return Results.Redirect("/login");
});

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
