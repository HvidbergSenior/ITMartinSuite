namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;

    public long TotalSizeBytes { get; set; }

    public List<MediaFile> Files { get; set; }
        = [];
}