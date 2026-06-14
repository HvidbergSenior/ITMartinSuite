using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class ResultMappingWorkflowStep
    : WorkflowStep<CardScanContext>
{
    public override string Name =>
        nameof(ResultMappingWorkflowStep);

    public override Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        context.State.Result =
            new CardScanResult
            {
                Name =
                    context.State.ScryfallMatchResult?.Name,

                SetCode =
                    context.State.ScryfallMatchResult?.SetCode,

                CollectorNumber =
                    context.State.ScryfallMatchResult?.CollectorNumber,

                ScryfallId =
                    context.State.ScryfallMatchResult?.ScryfallId,

                ImageUrl =
                    context.State.ScryfallMatchResult?.ImageUrl,

                EurPrice =
                    context.State.ScryfallMatchResult?.EurPrice,

                UsdPrice =
                    context.State.ScryfallMatchResult?.UsdPrice,

                Condition =
                    "Unknown",

                AdjustedEurValue =
                    context.State.ConditionResult?.AdjustedEurValue,

                AdjustedUsdValue =
                    context.State.ConditionResult?.AdjustedUsdValue,

                Confidence =
                    context.State.IdentificationConfidence,
                
                IsBlurry =
                    context.State.IsBlurry,

                NormalizedImagePath =
                    context.State.DetectedCardImagePath
                    ?? context.State.ImagePath
            };

        return Task.CompletedTask;
    }
}