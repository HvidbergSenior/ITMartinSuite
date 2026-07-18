namespace ITMartin.Ai.Models;

public sealed class ReceiptExtractionResult
{
    public string? MerchantName { get; set; }

    public string? PurchaseDate { get; set; }

    public decimal? TotalAmount { get; set; }

    public decimal? VatAmount { get; set; }

    public string? Currency { get; set; }

    public List<ReceiptLineItem> Items { get; set; } = [];

    public LoyaltyAccountInfo? LoyaltyAccount { get; set; }
}