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
        if (context.State.RecognitionResult is null)
        {
            throw new InvalidOperationException(
                "Recognition result missing.");
        }

        var match =
            await _scryfallService
                .SearchAsync(
                    context.State.OpenAiResult);

        if (match?.BestMatch is null)
        {
            throw new InvalidOperationException(
                "Scryfall match failed.");
        }

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
                    match.BestMatch.ImageUrl
            };
    }
}