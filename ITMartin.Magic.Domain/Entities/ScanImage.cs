public sealed class ScanImage
{
    public Guid Id { get; set; }

    public Guid ScanSessionId { get; set; }

    public ScanSession Session { get; set; } = null!;

    public string FilePath { get; set; } = string.Empty;

    public ScanStatus Status { get; set; }

    public ICollection<ScanResult> Results { get; set; } = [];
}