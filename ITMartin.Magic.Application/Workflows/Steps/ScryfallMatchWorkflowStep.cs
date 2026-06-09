using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class ScryfallMatchWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private const float OcrConfidenceThreshold =
        0.60f;

    private readonly IScryfallService
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
        // =====================================
        // IDENTIFICATION DATA
        // =====================================

        if (string.IsNullOrWhiteSpace(
                context.State.CardName))
        {
            return;
        }

        Console.WriteLine(
            $"CARD NAME: [{context.State.CardName}]");

        Console.WriteLine(
            $"COLLECTOR: [{context.State.CollectorNumber}]");

        if (context.State.OpenAiResult is not null)
        {
            Console.WriteLine(
                $"AI ARTIST: [{context.State.OpenAiResult.Artist}]");

            Console.WriteLine(
                $"AI SYMBOL: [{context.State.OpenAiResult.VisibleSetSymbolDescription}]");

            Console.WriteLine(
                $"AI SYMBOL VISIBLE: [{context.State.OpenAiResult.SetSymbolVisible}]");

            Console.WriteLine(
                $"AI WHITE BORDER: [{context.State.OpenAiResult.WhiteBorder}]");

            Console.WriteLine(
                $"AI OLD BORDER: [{context.State.OpenAiResult.OldBorder}]");

            Console.WriteLine(
                $"AI COPYRIGHT: [{context.State.OpenAiResult.CopyrightYear}]");
        }
        var match =
            await _scryfallService.SearchAsync(
                context.State.CardName,
                context.State.OpenAiResult,
                cancellationToken);
        
        if (match?.BestMatch is null)
        {
            return;
        }

        context.State.Candidates =
            match.Matches
                .Select(x => new CardCandidateViewModel
                {
                    Name = x.Card.Name,
                    SetCode = x.Card.Set,
                    CollectorNumber = x.Card.CollectorNumber,
                    ImageUrl = x.Card.ImageUrl,
                    EurPrice = x.Card.EurPrice,
                    UsdPrice = x.Card.UsdPrice,
                    Confidence = x.Confidence
                })
                .ToList();
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