using ITMartin.Media.Contracts.Contracts.Runtime.Models;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using ITMartin.OCR.Interfaces;

namespace ITMartin.Receipt.Application.Workflows.Steps;

public sealed class ReceiptOcrWorkflowStep
    : WorkflowStep<ReceiptContext>
{
    private readonly
        IGeneralOcrService
        _ocrService;

    public override string Name =>
        nameof(ReceiptOcrWorkflowStep);

    public ReceiptOcrWorkflowStep(
        IGeneralOcrService ocrService)
    {
        _ocrService =
            ocrService;
    }

    public override async Task ExecuteAsync(
        WorkflowExecutionContext<ReceiptContext> context,
        CancellationToken cancellationToken = default)
    {
        if (context.State.AdditionalImagePaths.Count > 0)
            // Multi-page receipt - combining OCR text coherently across separate
            // photos of a split receipt is unnecessary complexity when Claude can
            // already reason across multiple images directly (e.g. an item split
            // across the photo 1/photo 2 page break).
            return;

        try
        {
            var text = await _ocrService
                .ExtractTextAsync(
                    context.State.ImagePath,
                    cancellationToken);

            if (LooksLikeAReceipt(text))
                context.State.OcrText = text;
            // else: leave OcrText unset, same as the catch block below -
            // AiReceiptExtractionWorkflowStep falls back to sending the
            // image straight to Claude instead of trusting bad text.
        }
        catch
        {
            // OCR unavailable — AI step will use image directly
        }
    }

    // Tesseract runs against the raw, unprocessed camera photo (no deskew,
    // no crop, no contrast pass) - it can return non-empty text that's still
    // near-garbage for a photographed (not scanned) receipt, especially the
    // small item-row print, while the large header/total text OCRs fine.
    // Confirmed bug: a real JYSK receipt scan kept its correct merchant/date/
    // total (bold, large print) but lost every single line item, because
    // OCR "succeeded" (no exception) with unusable text for the small rows,
    // so the image-fallback path never triggered. A real receipt has many
    // separate price-shaped tokens - require a handful before trusting OCR
    // text over just letting Claude read the image directly.
    private static bool LooksLikeAReceipt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var priceMatches = System.Text.RegularExpressions.Regex.Matches(text, @"\d+[.,]\d{2}\b");
        return priceMatches.Count >= 3;
    }
}