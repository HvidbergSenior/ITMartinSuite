namespace ITMartin.Media.Domain.Models;

public sealed class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;

    public long TotalSizeBytes { get; set; }

    public IReadOnlyCollection<DuplicateFile> Files { get; set; }
        = [];
}