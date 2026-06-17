using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using Microsoft.Extensions.Logging;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class FinalScryfallMatchWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly IScryfallService
        _scryfallService;

    private readonly ILogger<FinalScryfallMatchWorkflowStep> _logger;

    public override string Name =>
        nameof(FinalScryfallMatchWorkflowStep);

    public FinalScryfallMatchWorkflowStep(
        IScryfallService scryfallService,
        ILogger<FinalScryfallMatchWorkflowStep> logger)
    {
        _scryfallService =
            scryfallService;
        _logger = logger;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                context.State.CardName))
        {
            return;
        }
        _logger.LogDebug("Scryfall match — set filter: {SetCode}", context.State.SetCode);
        var match =
            await _scryfallService.SearchAsync(
                context.State.CardName,
                context.State.SetCode,
                context.State.AiResult,
                cancellationToken);
        
        if (match?.BestMatch is null)
        {
            return;
        }

        context.State.Candidates =
        [
            new CardCandidateViewModel
            {
                Name =
                    match.BestMatch.Name,

                SetCode =
                    match.BestMatch.Set,

                CollectorNumber =
                    match.BestMatch.CollectorNumber,

                ImageUrl =
                    match.BestMatch.ImageUrl,

                EurPrice =
                    match.BestMatch.EurPrice,

                UsdPrice =
                    match.BestMatch.UsdPrice,

                Confidence =
                    context.State
                        .IdentificationConfidence
            }
        ];

        context.State.ScryfallMatchResult =
            new ScryfallMatchResult
            {
                Name =
                    match.BestMatch.Name,

                SetCode =
                    match.BestMatch.Set,

                CollectorNumber =
                    match.BestMatch.CollectorNumber,

                ScryfallId =
                    match.BestMatch.Id,

                ImageUrl =
                    match.BestMatch.ImageUrl,

                EurPrice =
                    match.BestMatch.EurPrice,

                EurFoilPrice =
                    match.BestMatch.EurFoilPrice,

                UsdPrice =
                    match.BestMatch.UsdPrice,

                UsdFoilPrice =
                    match.BestMatch.UsdFoilPrice
            };

        context.State.HasConfirmedMatch = true;

        context.State.OtherPrintings =
            match.Matches
                .Where(m =>
                    m.Card.Set != match.BestMatch.Set ||
                    m.Card.CollectorNumber != match.BestMatch.CollectorNumber)
                .OrderByDescending(m => m.Card.EurPrice ?? 0)
                .Select(m => new CardCandidateViewModel
                {
                    Name           = m.Card.Name,
                    SetCode        = m.Card.Set,
                    SetName        = m.Card.SetName ?? m.Card.Set,
                    CollectorNumber = m.Card.CollectorNumber,
                    ImageUrl       = m.Card.ImageUrl,
                    EurPrice       = m.Card.EurPrice,
                    EurFoilPrice   = m.Card.EurFoilPrice,
                    UsdPrice       = m.Card.UsdPrice,
                    UsdFoilPrice   = m.Card.UsdFoilPrice,
                    Confidence     = m.Confidence
                })
                .ToList();

        _logger.LogDebug(
            "Scryfall best match — {Name} [{Set}] #{Collector} EUR:{Eur} USD:{Usd}",
            match.BestMatch.Name,
            match.BestMatch.Set,
            match.BestMatch.CollectorNumber,
            match.BestMatch.EurPrice,
            match.BestMatch.UsdPrice);
    }
}