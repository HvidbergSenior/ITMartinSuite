using ITMartinBarTab.Server.Data;
using ITMartinBarTab.Server.Hubs;
using ITMartinBarTab.Server.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
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

builder.Services.AddSingleton<ConcurrencyService>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddScoped<CircuitHandler, ConcurrencyCircuitHandler>();

var app = builder.Build();

var maxUsers = app.Configuration.GetValue<int>("Concurrency:MaxUsers", 10);

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

// Redirect to queue page when too many concurrent users
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    var isPageRequest = ctx.Request.Headers.Accept.ToString().Contains("text/html");

    if (isPageRequest &&
        !path.StartsWithSegments("/queue.html") &&
        !path.StartsWithSegments("/_framework") &&
        !path.StartsWithSegments("/_blazor"))
    {
        var concurrency = ctx.RequestServices.GetRequiredService<ConcurrencyService>();
        if (concurrency.Active >= maxUsers)
        {
            var dest = Uri.EscapeDataString(path + ctx.Request.QueryString);
            ctx.Response.Redirect($"/queue.html?from={dest}");
            return;
        }
    }

    await next();
});

app.MapHub<SessionHub>("/hubs/session");

app.MapRazorComponents<ITMartinBarTab.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
