namespace ITMartin.Receipt.Application.Models;

public sealed class ReceiptTransactionItem
{
    public string Description { get; set; } = string.Empty;

    public decimal? Amount { get; set; }

    public string? Category { get; set; }
}