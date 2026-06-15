using ITMartin.Ai.Models;
using ITMartinLibrary.Application.Models;
using ITMartinLibrary.Application.Workflows;

namespace ITMartinLibrary.Application.Workflows;

public sealed class ShelfScanContext
{
    public required string ImagePath { get; init; }

    // =========================
    // PIPELINE RESULTS
    // =========================

    public LibraryShelfAnalysisResult? AiResult { get; set; }

    public ShelfScanResult? Result { get; set; }

    // =========================
    // EXECUTION
    // =========================

    public IList<ShelfWorkflowStep> Steps { get; } =
        new List<ShelfWorkflowStep>();

    public bool Failed { get; private set; }

    public string? FailureReason { get; private set; }

    public void Fail(string reason)
    {
        Failed = true;
        FailureReason = reason;
    }
}
