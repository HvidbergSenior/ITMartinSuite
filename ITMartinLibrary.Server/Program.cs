using ITMartinLibrary.Application.Interfaces;
using ITMartinLibrary.Application.Services;
using ITMartinLibrary.Infrastructure;
using ITMartinLibrary.Infrastructure.Repositories;
using ITMartinLibrary.Infrastructure.Services;
using ITMartinLibrary.Server;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddSingleton<IBarcodeEnrichmentQueue, BarcodeEnrichmentQueue>();
builder.Services.AddHostedService<BarcodeEnrichmentWorker>();
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlite("Data Source=library.db"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();