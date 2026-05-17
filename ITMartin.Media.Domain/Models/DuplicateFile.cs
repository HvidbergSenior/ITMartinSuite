namespace ITMartin.Media.Domain.Models;

public sealed class DuplicateFile
{
    public string Path { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ModifiedAt { get; set; }

    public bool IsOriginal { get; set; }
}