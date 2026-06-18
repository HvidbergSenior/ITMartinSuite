namespace ITMartin.Ai.Models;

public sealed class ReceiptLineItem
{
    public string Description { get; set; } = string.Empty;

    public decimal? Amount { get; set; }

    public bool Suspicious { get; set; }
}