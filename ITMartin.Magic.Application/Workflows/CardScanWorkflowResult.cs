using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanWorkflowResult
{
    public required bool Success { get; init; }

    public required CaptureResult? Result { get; init; }

    public required IReadOnlyCollection<WorkflowExecutionStep> Steps { get; init; }
}