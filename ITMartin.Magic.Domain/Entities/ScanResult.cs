public sealed class ScanResult
{
    public Guid Id { get; set; }

    public Guid ScanImageId { get; set; }

    public string CardName { get; set; } = string.Empty;

    public decimal Confidence { get; set; }
}