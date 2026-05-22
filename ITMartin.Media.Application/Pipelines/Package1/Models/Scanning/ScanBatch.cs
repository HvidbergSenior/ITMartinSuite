namespace ITMartin.Media.Application.Pipelines.Package1.Models.Scanning;

public sealed class ScanBatch
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public List<string> Files { get; set; }
        = [];

    public int BatchNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}