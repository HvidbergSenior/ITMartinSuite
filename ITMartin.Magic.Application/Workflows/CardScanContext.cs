using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Enums;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.OCR.Models;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanContext
{
    public required string ImagePath { get; init; }

    // =========================
    // GENERATED IMAGES
    // =========================

    public string? DetectedCardImagePath { get; set; }

    public string? PerspectiveCorrectedImagePath { get; set; }

    // =========================
    // PIPELINE RESULTS
    // =========================
    public MagicCardFrameType FrameType { get; set; } =
        MagicCardFrameType.Unknown;
    public CardLayoutType LayoutType { get; set; } =
        CardLayoutType.Unknown;

    public CardDetectionResult? DetectionResult { get; set; }

    public CardCornerDetectionResult? CardCornerResult { get; set; }

    public OcrRegionResult? OcrRegionResult { get; set; }

    public OcrResult? OcrResult { get; set; }

    public RecognitionResult? RecognitionResult { get; set; }

    public MagicCardAnalysisResult? OpenAiResult { get; set; }

    public ScryfallMatchResult? ScryfallMatchResult { get; set; }

    public CardScanResult? Result { get; set; }

    // =========================
    // EXECUTION
    // =========================

    public IList<WorkflowExecutionStep> Steps { get; } =
        new List<WorkflowExecutionStep>();

    public bool Failed { get; private set; }
    public bool IsBlurry { get; set; }

    public string? FailureReason { get; private set; }
    public CardConditionResult? ConditionResult { get; set; }

    public void Fail(string reason)
    {
        Failed = true;

        FailureReason = reason;
    }
    public List<CardCandidateViewModel>
        Candidates { get; set; } = [];
    
    public decimal AiConfidence =>
        OpenAiResult?.Confidence ?? 0;
}