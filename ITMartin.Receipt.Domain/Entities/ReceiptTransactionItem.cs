namespace ITMartin.Receipt.Domain.Entities;

public sealed class ReceiptTransactionItem
{
    public string Description { get; set; } = string.Empty;

    public decimal? Amount { get; set; }

    public string? Category { get; set; }

    public bool IsSuspicious { get; set; }

    public string? SuspicionReason { get; set; }
}
