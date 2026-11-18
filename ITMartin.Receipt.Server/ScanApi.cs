using ITMartin.OCR.Interfaces;
using ITMartin.Receipt.Application.Interfaces;
using ITMartin.Receipt.Application.Services;
using ITMartin.Receipt.Contracts.Scan.Responses;

namespace ITMartin.Receipt.Server;

// 2026-11-19 (Ugens indkøb: "kvittering?"): other apps send ONE receipt photo and get shop, total and date back.
// FREE FIRST: Tesseract OCR on this server + plain rules (ReceiptTotalReader) - no AI. Only when OCR cannot find the
// total AND a Claude key is set does it run the full scanner workflow (one Haiku read, saved here like any scan).
// Guarded by Receipt__ApiKey (X-Kvittering-Key) and a HARD daily cap on AI reads (CLAUDE.md cost rule).
public sealed class ScanCap(TimeProvider clock, int maxPerDay)
{
    private readonly object _gate = new();
    private DateOnly _day;
    private int _used;

    public bool TryUse()
    {
        lock (_gate)
        {
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            if (today != _day) { _day = today; _used = 0; }
            if (_used >= maxPerDay) return false;
            _used++;
            return true;
        }
    }
}

public static class ScanApi
{
    public const long MaxBytes = 12 * 1024 * 1024;

    public static void MapScanApi(this WebApplication app)
    {
        var key = app.Configuration["Receipt:ApiKey"] ?? "";
        var aiOn = !string.IsNullOrWhiteSpace(app.Configuration["Claude:ApiKey"]);
        var cap = new ScanCap(TimeProvider.System, app.Configuration.GetValue("Receipt:ApiMaxPerDay", 30));

        app.MapPost("/api/scan", async (HttpRequest req, IGeneralOcrService ocr, IReceiptWorkflowOrchestrator orchestrator, ILogger<ScanCap> log, CancellationToken ct) =>
        {
            if (key.Length < 16 || req.Headers["X-Kvittering-Key"] != key) return Results.StatusCode(401);
            if (!req.HasFormContentType) return Results.BadRequest(new ScanReceiptResponse { FailureReason = "Send billedet som en fil (multipart)." });
            var form = await req.ReadFormAsync(ct);
            var file = form.Files.FirstOrDefault();
            if (file is null || file.Length == 0) return Results.BadRequest(new ScanReceiptResponse { FailureReason = "Der kom intet billede med." });
            if (file.Length > MaxBytes) return Results.BadRequest(new ScanReceiptResponse { FailureReason = "Billedet er for stort (højst 12 MB)." });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant() is ".png" or ".webp" ? Path.GetExtension(file.FileName).ToLowerInvariant() : ".jpg";
            Directory.CreateDirectory(Path.Combine("data", "receipts"));
            var path = Path.Combine("data", "receipts", $"api-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40] + ext);
            await using (var fs = File.Create(path)) await file.CopyToAsync(fs, ct);

            // 1) Free: OCR + rules.
            OcrReceiptFacts facts;
            try { facts = ReceiptTotalReader.Read(await ocr.ExtractTextAsync(path, ct)); }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogWarning(e, "OCR failed"); facts = new(null, null, null); }
            if (facts.Total is > 0)
                return Results.Ok(new ScanReceiptResponse { Success = true, MerchantName = facts.Merchant, PurchaseDate = facts.Date, TotalAmount = facts.Total });

            // 2) Only if allowed and still unknown: one AI read through the normal workflow.
            if (!aiOn) return Results.Ok(new ScanReceiptResponse { MerchantName = facts.Merchant, FailureReason = "Totalen kunne ikke læses. Prøv et skarpere billede af bunden, eller skriv beløbet selv." });
            if (!cap.TryUse()) return Results.Json(new ScanReceiptResponse { FailureReason = "Dagens kvote af kvitteringer er brugt. Skriv beløbet selv i dag." }, statusCode: 429);
            var result = await orchestrator.ExecuteAsync(path, ct);
            var x = result.ExtractionResult;
            if (result.Failed || x is null || x.TotalAmount is not > 0)
                return Results.Ok(new ScanReceiptResponse { MerchantName = x?.MerchantName ?? facts.Merchant, FailureReason = result.FailureReason ?? "Totalen kunne ikke læses. Skriv beløbet selv." });
            return Results.Ok(new ScanReceiptResponse
            {
                Success = true, MerchantName = x.MerchantName, TotalAmount = x.TotalAmount, Currency = x.Currency ?? "DKK",
                PurchaseDate = DateTime.TryParse(x.PurchaseDate, out var d) ? d : facts.Date,
                Items = x.Items.Select(i => new ScanReceiptLineItem { Description = i.Description ?? "", Amount = i.Amount }).ToList(),
            });
        }).DisableAntiforgery();
    }
}
