using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class ScryfallMatchWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        IScryfallService
        _scryfallService;

    public override string Name =>
        nameof(ScryfallMatchWorkflowStep);

    public ScryfallMatchWorkflowStep(
        IScryfallService scryfallService)
    {
        _scryfallService =
            scryfallService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            context.State.OpenAiResult);
        Console.WriteLine(
            $"AI Name: [{context.State.OpenAiResult?.Name}]");

        Console.WriteLine(
            $"AI Set: [{context.State.OpenAiResult?.SetCode}]");

        Console.WriteLine(
            $"AI Collector: [{context.State.OpenAiResult?.CollectorNumber}]");
        var match =
            await _scryfallService
                .SearchAsync(
                    context.State.OpenAiResult,
                    cancellationToken);

        if (match?.BestMatch is null)
        {
            throw new InvalidOperationException(
                $"No Scryfall match found for '{context.State.OpenAiResult?.Name}'.");
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
                        .OpenAiResult?
                        .Confidence ?? 0
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