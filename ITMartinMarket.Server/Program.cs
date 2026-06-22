using ITMartinMarket.Application.Interfaces;
using ITMartinMarket.Infrastructure;
using ITMartinMarket.Server.Components;
using ITMartinMarket.Server.Hubs;
using ITMartinMarket.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 5 * 1024 * 1024);
builder.Services.AddSignalR();
builder.Services.AddMarketInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ToastService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MarketDbContext>();
    await db.Database.EnsureCreatedAsync();
}

Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "data", "items"));

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStaticFiles();
app.UseAntiforgery();

app.MapHub<MarketHub>("/hubs/market");

app.MapGet("/item-image/{id:guid}", async (Guid id, ISaleItemRepository repo) =>
{
    var item = await repo.GetByIdAsync(id);
    if (item?.ImagePath is null || !File.Exists(item.ImagePath)) return Results.NotFound();
    return Results.File(await File.ReadAllBytesAsync(item.ImagePath), "image/jpeg");
});

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
