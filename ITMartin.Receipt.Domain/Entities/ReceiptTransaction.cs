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
}
