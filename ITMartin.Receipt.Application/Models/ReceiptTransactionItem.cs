namespace ITMartin.Receipt.Application.Models;

public sealed class ReceiptTransactionItem
{
    public string Description { get; set; } = string.Empty;

    public decimal? OriginalPrice { get; set; }

    public decimal? DiscountAmount { get; set; }

    public string? DiscountType { get; set; }

    public string? RawText { get; set; }

    public bool IsSuspicious { get; set; }
}