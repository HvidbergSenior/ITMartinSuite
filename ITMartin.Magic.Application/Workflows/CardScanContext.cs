using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Models;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanContext
{
    public required Guid SessionId { get; init; }

    public required Guid JobId { get; init; }

    public required string ImagePath { get; init; }

    // =========================
    // PIPELINE STATE
    // =========================

    public CardDetectionResult? DetectionResult { get; set; }

    public CardCornerDetectionResult? CornerResult { get; set; }

    public string? CorrectedImagePath { get; set; }

    public OcrRegionResult? OcrResult { get; set; }

    public CaptureResult? CaptureResult { get; set; }

    // =========================
    // EXECUTION
    // =========================

    public IList<WorkflowExecutionStep> Steps { get; } =
        new List<WorkflowExecutionStep>();

    public bool Failed { get; private set; }

    public string? FailureReason { get; private set; }

    public void Fail(string reason)
    {
        Failed = true;

        FailureReason = reason;
    }
}