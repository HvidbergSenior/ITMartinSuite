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
            context.State.Fail(
                "Kunne ikke læse et kortnavn på billedet. Prøv at holde kortet fladt, tættere på kameraet og i godt lys.");
            return;
        }
        _logger.LogInformation("Scryfall match — set filter: {SetCode}", context.State.SetCode);
        var match =
            await _scryfallService.SearchAsync(
                context.State.CardName,
                context.State.SetCode,
                context.State.AiResult,
                cancellationToken);

        if (match?.BestMatch is null)
        {
            context.State.Fail(
                $"Læste navnet \"{context.State.CardName}\", men fandt ingen matchende udgave på Scryfall. Tjek om navnet blev læst korrekt, eller om det valgte sæt er forkert.");
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

                SetName =
                    match.BestMatch.SetName ?? match.BestMatch.Set,

                ReleasedAt =
                    match.BestMatch.ReleasedAt,

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

        // If another printing scored identically to the best match, the AI didn't
        // extract enough distinguishing detail (usually copyright year on old
        // reprints) to actually tell them apart — this pick is a guess, not a
        // confirmed identification. Surface that instead of presenting it as fact.
        var topScore = match.Matches.Count > 0 ? match.Matches[0].Score : 0;
        var isAmbiguous = match.Matches
            .Skip(1)
            .Any(m => m.Score == topScore &&
                      (m.Card.Set != match.BestMatch.Set || m.Card.CollectorNumber != match.BestMatch.CollectorNumber));

        context.State.ScryfallMatchResult =
            new ScryfallMatchResult
            {
                Name =
                    match.BestMatch.Name,

                SetCode =
                    match.BestMatch.Set,

                SetName =
                    match.BestMatch.SetName ?? match.BestMatch.Set,

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
                    match.BestMatch.UsdFoilPrice,

                IsAmbiguous =
                    isAmbiguous
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
                    ReleasedAt     = m.Card.ReleasedAt,
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