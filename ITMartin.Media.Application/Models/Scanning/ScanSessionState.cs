namespace ITMartin.Media.Application.Models.Scan;

public sealed class ScanSessionState
{
    public Guid SessionId { get; set; }

    public string RootPath { get; set; } = string.Empty;

    public int CurrentStepIndex { get; set; }

    public List<string> ProcessedFiles { get; set; }
        = [];

    public DateTimeOffset UpdatedAt { get; set; }
}