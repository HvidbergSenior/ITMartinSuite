using ITMartin.Magic.Application.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.OCR.Interfaces;

namespace ITMartin.Magic.Application.Workflows.Steps;

public sealed class OcrWorkflowStep
    : IWorkflowStep
{
    private readonly
        IOcrRegionExtractor
        _ocrRegionExtractor;

    private readonly
        IOcrService
        _ocrService;

    public string Name =>
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

    public async Task ExecuteAsync<TState>(
        WorkflowExecutionContext<TState> context,
        CancellationToken cancellationToken = default)
        where TState : class
    {
        var state =
            context.State as CardScanWorkflowState
            ?? throw new InvalidOperationException(
                "Invalid workflow state.");

        var imagePath =
            state.CorrectedImagePath
            ?? state.ImagePath;

        var region =
            await _ocrRegionExtractor
                .ExtractAsync(imagePath);

        if (region is null)
        {
            throw new InvalidOperationException(
                "OCR region extraction failed.");
        }

        var result =
            await _ocrService
                .ExtractTextAsync(
                    region.ImagePath);

        if (string.IsNullOrWhiteSpace(result))
        {
            throw new InvalidOperationException(
                "OCR failed.");
        }

        state.OcrResult =
            result;
    }
}