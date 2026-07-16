namespace ITMartin.Media.Contracts.Contracts.Runtime.Models;

public sealed class StaticGalleryExportResult
{
    public int TotalFiles { get; init; }
    public int ThumbnailsGenerated { get; init; }
    public int YearsGenerated { get; init; }
    public required string IndexPath { get; init; }
}
