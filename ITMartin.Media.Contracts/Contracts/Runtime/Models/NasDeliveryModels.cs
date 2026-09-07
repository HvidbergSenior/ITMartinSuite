namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class NasPushResult
{
    public bool Success { get; init; }
    public string RemotePath { get; init; } = string.Empty;
    public string? Error { get; init; }
}

public sealed class GalleryWireResult
{
    public bool Success { get; init; }
    public bool AlreadyWired { get; init; }
    public int? AssignedIndex { get; init; }
    public string? Error { get; init; }
}
