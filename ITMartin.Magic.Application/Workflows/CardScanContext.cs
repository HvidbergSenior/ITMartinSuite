using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanContext
{
    public required string ImagePath { get; init; }

    // =========================
    // GENERATED IMAGES
    // =========================
    public string? CardName { get; set; }

    public decimal IdentificationConfidence { get; set; }
    public string? DetectedCardImagePath { get; set; }

    public string? CollectorNumber { get; set; }

    public string? SetSymbolDescription { get; set; }

    public string? Artist { get; set; }

    public string? CopyrightYear { get; set; }

    // =========================
    // PIPELINE RESULTS
    // =========================

    public MagicCardAnalysisResult? AiResult { get; set; }

    public ScryfallMatchResult? ScryfallMatchResult { get; set; }
    public bool HasConfirmedMatch { get; set; }

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

    public string? SetCode { get; init; }
    
}