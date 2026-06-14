using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class FinalScryfallMatchWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly IScryfallService
        _scryfallService;

    public override string Name =>
        nameof(FinalScryfallMatchWorkflowStep);

    public FinalScryfallMatchWorkflowStep(
        IScryfallService scryfallService)
    {
        _scryfallService =
            scryfallService;
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
        Console.WriteLine(
            $"SET FILTER: {context.State.SetCode}");
        var match =
            await _scryfallService.SearchAsync(
                context.State.CardName,
                context.State.SetCode,
                context.State.OpenAiResult,
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

        context.State.HasConfirmedMatch =
            true;

        Console.WriteLine(
            $"CARD: {match.BestMatch.Name}");

        Console.WriteLine(
            $"EUR: {match.BestMatch.EurPrice}");

        Console.WriteLine(
            $"USD: {match.BestMatch.UsdPrice}");

        Console.WriteLine(
            $"SET: {match.BestMatch.Set}");

        Console.WriteLine(
            $"COLLECTOR: {match.BestMatch.CollectorNumber}");
    }
}