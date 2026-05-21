using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class SaveDebugImagesWorkflowStep
    : WorkflowStep<CardScanContext>
{
    public override string Name =>
        nameof(SaveDebugImagesWorkflowStep);

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        var debugDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "magic-debug",
                Guid.NewGuid().ToString());

        Directory.CreateDirectory(
            debugDirectory);

        if (File.Exists(context.State.ImagePath))
        {
            File.Copy(
                context.State.ImagePath,
                Path.Combine(
                    debugDirectory,
                    "01-original.jpg"),
                true);
        }

        if (!string.IsNullOrWhiteSpace(
                context.State.DetectedCardImagePath)
            && File.Exists(
                context.State.DetectedCardImagePath))
        {
            File.Copy(
                context.State.DetectedCardImagePath,
                Path.Combine(
                    debugDirectory,
                    "02-detected-card.jpg"),
                true);
        }

        if (!string.IsNullOrWhiteSpace(
                context.State.PerspectiveCorrectedImagePath)
            && File.Exists(
                context.State.PerspectiveCorrectedImagePath))
        {
            File.Copy(
                context.State.PerspectiveCorrectedImagePath,
                Path.Combine(
                    debugDirectory,
                    "03-perspective-corrected.jpg"),
                true);
        }

        await Task.CompletedTask;
    }
}