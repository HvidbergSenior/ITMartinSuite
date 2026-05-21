using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Models;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanWorkflowState
{
    public required string ImagePath { get; init; }

    public CardDetectionResult? DetectionResult { get; set; }

    public CardCornerDetectionResult? CornerResult { get; set; }

    public string? CorrectedImagePath { get; set; }

    public OcrRegionResult? OcrRegionResult { get; set; }

    public OcrTextResult? OcrTextResult { get; set; }

    public CaptureResult? CaptureResult { get; set; }
}