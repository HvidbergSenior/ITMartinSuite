using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ITMartin.Magic.Infrastructure.Services;

public sealed class MagicPriceAlertService(
    IServiceScopeFactory scopeFactory,
    ILogger<MagicPriceAlertService> logger) : BackgroundService
{
    private const decimal AlertThreshold = 2.0m;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once shortly after startup, then every 24h
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCheckAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task RunCheckAsync(CancellationToken ct)
    {
        logger.LogInformation("Magic price alert check starting");

        using var scope = scopeFactory.CreateScope();
        var cards = scope.ServiceProvider.GetRequiredService<IMagicCardRepository>();
        var alerts = scope.ServiceProvider.GetRequiredService<IPriceAlertRepository>();
        var scryfall = scope.ServiceProvider.GetRequiredService<IScryfallService>();

        var collection = (await cards.GetAllAsync(ct)).ToList();

        foreach (var card in collection)
        {
            if (string.IsNullOrWhiteSpace(card.ScryfallId)) continue;

            // Throttle to avoid hitting Scryfall rate limits
            await Task.Delay(100, ct);

            var prices = await scryfall.GetPriceByIdAsync(card.ScryfallId, ct);
            if (prices is null) continue;

            var newEur = prices.Value.Eur;
            if (newEur is null || card.EurPrice is null) continue;

            var delta = newEur.Value - card.EurPrice.Value;

            if (delta >= AlertThreshold)
            {
                logger.LogInformation(
                    "Price alert: {Card} jumped from €{Old} to €{New}",
                    card.Name, card.EurPrice, newEur);

                await alerts.AddAsync(new PriceAlert
                {
                    CardName = card.Name,
                    SetCode = card.SetCode,
                    OldPrice = card.EurPrice.Value,
                    NewPrice = newEur.Value,
                    Delta = delta
                }, ct);
            }

            // Update stored price regardless
            card.EurPrice = newEur;
            card.UsdPrice = prices.Value.Usd;
            card.LastSeenAt = DateTime.UtcNow;
            await cards.UpdateAsync(card, ct);
        }

        logger.LogInformation("Magic price alert check complete — {Count} cards checked", collection.Count);
    }
}
