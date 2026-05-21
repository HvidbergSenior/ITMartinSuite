using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Application.Workflows.Steps;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Magic.Application.Workflows;

public sealed class PerspectiveCorrectionWorkflowStep
    : IWorkflowStep
{
    private readonly
        IPerspectiveCorrectionService
        _perspectiveCorrectionService;
    public string Name =>
        nameof(DetectCardWorkflowStep);

    public PerspectiveCorrectionWorkflowStep(
        IPerspectiveCorrectionService
            perspectiveCorrectionService)
    {
        _perspectiveCorrectionService =
            perspectiveCorrectionService;
    }

    public async Task ExecuteAsync(
        CardScanContext context,
        CancellationToken cancellationToken)
    {
        if (context.CornerResult is null)
        {
            context.Fail(
                "Card corners were not detected.");

            return;
        }

        var correctedImagePath =
            await _perspectiveCorrectionService
                .CorrectPerspectiveAsync(
                    context.ImagePath,
                    context.CornerResult);

        if (string.IsNullOrWhiteSpace(
                correctedImagePath))
        {
            context.Fail(
                "Perspective correction failed.");

            return;
        }

        context.CorrectedImagePath =
            correctedImagePath;
    }
}