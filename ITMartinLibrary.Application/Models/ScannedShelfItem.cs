namespace ITMartinLibrary.Application.Models;

public sealed class ScannedShelfItem
{
    public string? Title { get; set; }

    public string? Author { get; set; }

    public string? Barcode { get; set; }

    public string? Isbn { get; set; }

    public string MediaType { get; set; } = "Unknown";

    public decimal Confidence { get; set; }

    public string? CoverUrl { get; set; }

    public bool AddedToInventory { get; set; }
}
