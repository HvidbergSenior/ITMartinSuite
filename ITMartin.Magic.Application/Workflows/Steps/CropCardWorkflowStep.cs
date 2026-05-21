using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class CropCardWorkflowStep
    : WorkflowStep<CardScanContext>
{
    public override string Name =>
        nameof(CropCardWorkflowStep);

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State.DetectionResult is null)
        {
            throw new InvalidOperationException(
                "Detection result missing.");
        }

        // TODO:
        // Crop image using detection result
        // Save cropped image to disk

        context.State.DetectedCardImagePath =
            "cropped-image-path";

        await Task.CompletedTask;
    }
}