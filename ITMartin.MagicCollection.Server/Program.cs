using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure;
using ITMartin.Magic.Infrastructure.Persistence;
using ITMartin.MagicCollection.Server;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

var connectionString = builder.Configuration.GetConnectionString("MagicDb")
    ?? "Host=localhost;Database=magic;Username=postgres";

builder.Services.AddMagicPersistence(connectionString);
builder.Services.AddScoped<IMagicCardRepository, MagicCardRepository>();
builder.Services.AddScoped<IPriceAlertRepository, PriceAlertRepository>();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MagicDbContext>();
    await db.Database.MigrateAsync();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
