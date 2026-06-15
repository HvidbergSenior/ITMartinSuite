using ITMartin.Ai;
using ITMartinLibrary.Application;
using ITMartinLibrary.Infrastructure;
using ITMartinLibrary.Infrastructure.Services;
using ITMartinLibrary.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// =========================
// CORE SERVICES
// =========================

builder.Services.AddLibraryApplication();
builder.Services.AddLibraryInfrastructure(builder.Configuration);
builder.Services.AddAi();

builder.Services.AddHostedService<BarcodeEnrichmentWorker>();

// =========================
// SIGNALR
// =========================

builder.Services.Configure<HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024 * 20;
});

// =========================
// BLAZOR
// =========================

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// =========================
// BUILD
// =========================

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
    await db.Database.EnsureCreatedAsync();
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
