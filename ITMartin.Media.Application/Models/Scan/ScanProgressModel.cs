namespace ITMartin.Media.Application.Models.Scan;

public sealed class ScanProgressModel
{
    public Guid SessionId { get; set; }

    public long FilesDiscovered { get; set; }

    public long FilesProcessed { get; set; }

    public long FilesFailed { get; set; }

    public double ProgressPercentage { get; set; }

    public string CurrentStage { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}