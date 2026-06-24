using ITMartinTestHub.Server.Controllers;
using ITMartinTestHub.Server.Data;
using ITMartinTestHub.Server.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddControllers();

var dbPath = builder.Configuration.GetConnectionString("TestHubDb")
    ?? "Data Source=/app/db/testhub.db";

builder.Services.AddDbContext<TestHubDbContext>(o => o.UseSqlite(dbPath));
builder.Services.AddScoped<TestHubService>();
builder.Services.AddSingleton<ToastService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TestHubDbContext>();
    db.Database.EnsureCreated();

    // Add Purpose column if it doesn't exist yet (EnsureCreated won't migrate existing DBs)
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Assignments\" ADD COLUMN \"Purpose\" TEXT NULL"); }
    catch { }

    await SeedService.SeedAppsAsync(db);
    await SeedService.UpdateAppUrlsAsync(db);
    await SeedService.SeedStepsAsync(db);
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

// Guard all /admin/* routes — redirect to login if cookie is missing or wrong
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    if (path.StartsWithSegments("/admin") && !path.StartsWithSegments("/admin/login"))
    {
        var pin  = app.Configuration["Admin:Pin"] ?? "1234";
        var want = AdminAuthController.Token(pin);
        var got  = ctx.Request.Cookies["th_admin"];
        if (got != want)
        {
            ctx.Response.Redirect("/admin/login");
            return;
        }
    }
    await next();
});

app.MapRazorComponents<ITMartinTestHub.Server.App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();

app.Run();
