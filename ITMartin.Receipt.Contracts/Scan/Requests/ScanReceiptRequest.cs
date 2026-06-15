namespace ITMartin.Receipt.Contracts.Scan.Requests;

public sealed class ScanReceiptRequest
{
    public required string ImagePath { get; init; }
}
