using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.OCR.Interfaces;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class OcrWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly IOcrRegionExtractor
        _ocrRegionExtractor;

    private readonly IOcrService
        _ocrService;

    public override string Name =>
        nameof(OcrWorkflowStep);

    public OcrWorkflowStep(
        IOcrRegionExtractor ocrRegionExtractor,
        IOcrService ocrService)
    {
        _ocrRegionExtractor =
            ocrRegionExtractor;

        _ocrService =
            ocrService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<CardScanContext> context,
        CancellationToken cancellationToken = default)
    {
        var imagePath =
            context.State.PerspectiveCorrectedImagePath
            ?? context.State.DetectedCardImagePath
            ?? context.State.ImagePath;
        if (context.State.FrameType ==
            MagicCardFrameType.OldBorder)
        {
            Console.WriteLine(
                "Skipping OCR for old border card");

            return;
        }
        Console.WriteLine(
            $"INPUT IMAGE: {context.State.ImagePath}");
        var ocrRegionResult =
            await _ocrRegionExtractor
                .ExtractAsync(
                    imagePath,
                    cancellationToken);

        context.State.OcrRegionResult =
            ocrRegionResult;

        context.State.OcrResult =
            await _ocrService
                .ExtractTextAsync(
                    ocrRegionResult,
                    cancellationToken);
    }
}