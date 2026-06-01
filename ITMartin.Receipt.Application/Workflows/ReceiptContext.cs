using ITMartin.Ai.Models;
using ITMartin.Receipt.Application.Models;

namespace ITMartin.Receipt.Application.Workflows;

public sealed class ReceiptContext
{
    public required string ImagePath { get; init; }

    public string? OcrText { get; set; }

    public ReceiptExtractionResult? ExtractionResult { get; set; }

    public bool Failed { get; private set; }

    public string? FailureReason { get; private set; }

    public void Fail(string reason)
    {
        Failed = true;
        FailureReason = reason;
    }
    public ReceiptTransaction? Transaction { get; set; }
}