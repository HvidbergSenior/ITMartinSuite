using ITMartin.Ai.Models;
using ITMartin.Receipt.Application.Models;

namespace ITMartin.Receipt.Application.Workflows;

public sealed class ReceiptContext
{
    public required string ImagePath { get; init; }

    // Extra photos for a receipt too long to fit legibly in one shot (e.g.
    // a long thermal receipt) - ImagePath stays the "cover" image used for
    // ImageFileName/display, these are additional pages sent alongside it
    // for extraction. Empty for the common single-photo case.
    public List<string> AdditionalImagePaths { get; init; } = [];

    public string? ItemsPhotoPath { get; init; }

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