namespace ITMartin.Media.Application.Models.Scanning;

public sealed class ScanSession
{
    public Guid Id { get; set; }

    public string RootPath { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int TotalFiles { get; set; }

    public int ProcessedFiles { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}