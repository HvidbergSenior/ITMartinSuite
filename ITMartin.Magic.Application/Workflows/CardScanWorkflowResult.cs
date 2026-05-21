using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Workflows;

public sealed class CardScanWorkflowResult
{
    public required bool Success { get; init; }

    public required CardRecognitionResult? Result { get; init; }

    public required IReadOnlyCollection<WorkflowExecutionStep> Steps { get; init; }
}