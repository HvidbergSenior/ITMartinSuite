using System.Text.Json;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Workflows;
using ITMartin.Magic.Domain.Entities;
using ITMartin.Media.Application.Abstractions.BackgroundJobs;
using ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;
using Microsoft.Extensions.Logging;

namespace ITMartin.Magic.Infrastructure.BackgroundJobs;

public sealed class ProcessMediaHandler
    : IBackgroundJobHandler
{
    private readonly CardScanWorkflowRunner _runner;
    private readonly IMagicCardRepository _cardRepository;
    private readonly ILogger<ProcessMediaHandler> _logger;

    public string JobType =>
        BackgroundJobTypes.ProcessMedia;

    public ProcessMediaHandler(
        CardScanWorkflowRunner runner,
        IMagicCardRepository cardRepository,
        ILogger<ProcessMediaHandler> logger)
    {
        _runner = runner;
        _cardRepository = cardRepository;
        _logger = logger;
    }

    public async Task HandleAsync(
        BackgroundJob job,
        CancellationToken cancellationToken)
    {
        var payload =
            JsonSerializer.Deserialize<CardScanJobPayload>(
                job.Payload);

        _logger.LogDebug(
            "Processing card scan job — set: {SetCode}",
            payload?.SetCode);

        if (payload is null)
            throw new InvalidOperationException("Invalid payload.");

        var context = new CardScanContext
        {
            ImagePath = payload.ImagePath,
            SetCode = payload.SetCode
        };

        await _runner.ExecuteAsync(context, cancellationToken);

        if (context.Result is not { } result ||
            string.IsNullOrWhiteSpace(result.ScryfallId))
        {
            _logger.LogDebug(
                "Card scan produced no identifiable result — skipping save");
            return;
        }

        var existing =
            await _cardRepository.GetByScryfallIdAsync(
                result.ScryfallId,
                cancellationToken);

        if (existing is not null)
        {
            existing.Quantity++;
            existing.EurPrice = result.EurPrice;
            existing.UsdPrice = result.UsdPrice;
            existing.LastSeenAt = DateTime.UtcNow;

            await _cardRepository.UpdateAsync(existing, cancellationToken);

            _logger.LogInformation(
                "Updated {Name} [{Set}] — qty: {Qty} EUR: {Eur} USD: {Usd}",
                existing.Name, existing.SetCode, existing.Quantity,
                existing.EurPrice, existing.UsdPrice);
        }
        else
        {
            var card = new MagicCard
            {
                Id = Guid.NewGuid(),
                Name = result.Name ?? "Unknown",
                SetCode = result.SetCode ?? string.Empty,
                CollectorNumber = result.CollectorNumber ?? string.Empty,
                ScryfallId = result.ScryfallId,
                Quantity = 1,
                EurPrice = result.EurPrice,
                UsdPrice = result.UsdPrice,
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };

            await _cardRepository.AddAsync(card, cancellationToken);

            _logger.LogInformation(
                "Saved new card {Name} [{Set}] #{Collector} EUR: {Eur} USD: {Usd}",
                card.Name, card.SetCode, card.CollectorNumber,
                card.EurPrice, card.UsdPrice);
        }
    }
}
