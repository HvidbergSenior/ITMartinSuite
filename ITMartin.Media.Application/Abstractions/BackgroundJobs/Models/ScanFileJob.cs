namespace ITMartin.Media.Application.Abstractions.BackgroundJobs.Models;

public sealed class ScanFileJob
{
    public Guid SessionId { get; set; }

    public string FilePath { get; set; } = string.Empty;
}