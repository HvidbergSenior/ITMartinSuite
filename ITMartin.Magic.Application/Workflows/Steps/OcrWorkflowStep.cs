using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.OCR.Interfaces;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class OcrWorkflowStep
    : WorkflowStep<CardScanContext>
{
    private readonly
        IOcrRegionExtractor
        _ocrRegionExtractor;

    private readonly
        IOcrService
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
        if (context.State.PerspectiveCorrectedImagePath is null)
        {
            throw new InvalidOperationException(
                "Perspective corrected image missing.");
        }

        var regions =
            await _ocrRegionExtractor
                .ExtractAsync(
                    context.State.PerspectiveCorrectedImagePath);

        if (regions is null)
        {
            throw new InvalidOperationException(
                "OCR region extraction failed.");
        }

        context.State.OcrRegionResult =
            regions;

        var result =
            await _ocrService
                .ExtractTextAsync(
                    regions);

        if (result is null)
        {
            throw new InvalidOperationException(
                "OCR failed.");
        }

        context.State.OcrResult =
            result;
    }
}