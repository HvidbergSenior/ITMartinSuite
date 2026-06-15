namespace ITMartin.Library.Contracts.Scan.Responses;

public sealed class ScanShelfResponse
{
    public bool Success { get; init; }

    public string? FailureReason { get; init; }

    public List<ScanShelfItem> Items { get; init; } = [];
}

public sealed class ScanShelfItem
{
    public string? Title { get; init; }

    public string? Author { get; init; }

    public string? Barcode { get; init; }

    public string? Isbn { get; init; }

    public string MediaType { get; init; } = "Unknown";

    public decimal Confidence { get; init; }
}
