namespace ITMartin.Receipt.Contracts.Scan.Responses;

public sealed class ScanReceiptResponse
{
    public bool Success { get; init; }

    public string? FailureReason { get; init; }

    public string? MerchantName { get; init; }

    public DateTime? PurchaseDate { get; init; }

    public decimal? TotalAmount { get; init; }

    public string Currency { get; init; } = "DKK";

    public List<ScanReceiptLineItem> Items { get; init; } = [];
}

public sealed class ScanReceiptLineItem
{
    public string Description { get; init; } = string.Empty;

    public decimal? Amount { get; init; }

    public string? Category { get; init; }
}
