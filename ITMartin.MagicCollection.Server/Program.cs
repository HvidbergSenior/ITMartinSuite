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

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/collection", async (IMagicCardRepository cards, IPriceAlertRepository alerts) =>
{
    var allCards   = (await cards.GetAllAsync()).ToList();
    var allAlerts  = await alerts.GetActiveAsync();
    var totalValue = allCards.Sum(c => (c.EurPrice ?? 0) * c.Quantity);

    return Results.Ok(new
    {
        totalValue,
        cards  = allCards.Select(c => new
        {
            c.Id, c.Name, c.SetCode, c.CollectorNumber, c.Quantity,
            eurPrice   = c.EurPrice,
            totalEur   = c.EurPrice.HasValue ? c.EurPrice.Value * c.Quantity : (decimal?)null,
            lastSeenAt = c.LastSeenAt,
        }),
        alerts = allAlerts.Select(a => new
        {
            a.Id, a.CardName, a.SetCode, a.OldPrice, a.NewPrice, a.Delta, a.DetectedAt
        }),
    });
});

app.MapPost("/api/alerts/{id:guid}/dismiss", async (Guid id, IPriceAlertRepository alerts) =>
{
    await alerts.DismissAsync(id);
    return Results.Ok();
});

app.Run();
