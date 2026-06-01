using ITMartin.Ai.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class CardConditionWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        ICardConditionAnalysisService
        _conditionService;

    public override string Name =>
        nameof(CardConditionWorkflowStep);

    public CardConditionWorkflowStep(
        ICardConditionAnalysisService conditionService)
    {
        _conditionService =
            conditionService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        var match =
            context.State.ScryfallMatchResult
            ?? throw new InvalidOperationException(
                "Missing Scryfall match.");

        var result =
            await _conditionService.AnalyzeAsync(
                context.State.ImagePath,
                null,
                null);

        context.State.ConditionResult =
            result;
    }
}