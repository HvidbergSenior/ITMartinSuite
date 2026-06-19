namespace ITMartin.Ai.Models;

public sealed record LibraryShelfItem
{
    public string? Title { get; init; }

    public string? Author { get; init; }

    public string? Isbn { get; init; }

    public string? Barcode { get; init; }

    public string? MediaType { get; init; }

    public decimal Confidence { get; init; }

    // Bounding box as percentage of image dimensions (0-100). Null when Claude cannot determine position.
    public double? BBoxX { get; init; }
    public double? BBoxY { get; init; }
    public double? BBoxW { get; init; }
    public double? BBoxH { get; init; }
}