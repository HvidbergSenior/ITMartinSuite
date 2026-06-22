using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure;
using ITMartin.Magic.Infrastructure.Persistence;

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

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ITMartin.MagicCollection.Server.App>()
    .AddInteractiveServerRenderMode();

app.Run();
