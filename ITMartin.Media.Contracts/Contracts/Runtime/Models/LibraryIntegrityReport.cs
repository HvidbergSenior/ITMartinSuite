namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class IntegrityFailure
{
    public string RelativePath { get; init; } = string.Empty;
    public string MediaType { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class LibraryIntegrityReport
{
    public int TotalFilesChecked { get; init; }
    public int FailureCount { get; init; }
    public List<IntegrityFailure> Failures { get; init; } = [];
    public DateTime CheckedAtUtc { get; init; } = DateTime.UtcNow;
}
