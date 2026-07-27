namespace ITMartin.Receipt.Domain.Entities;

public sealed class ReceiptTransaction
{
    public Guid Id { get; init; }

    public string MerchantName { get; set; } = string.Empty;

    public DateTime? PurchaseDate { get; set; }

    public decimal? TotalAmount { get; set; }

    public decimal? VatAmount { get; set; }

    public string Currency { get; set; } = "DKK";

    public List<ReceiptTransactionItem> Items { get; set; } = [];

    public DateTime ScannedAt { get; init; } = DateTime.UtcNow;

    public bool IsTemplate { get; set; }

    public string? ImageFileName { get; set; }

    // Optional photo of the physical items, taken before scanning the
    // receipt, so the user can compare what they actually bought against
    // what the OCR/AI extracted - a manual cross-check for blurry or
    // hard-to-read receipts.
    public string? ItemsPhotoFileName { get; set; }
}
