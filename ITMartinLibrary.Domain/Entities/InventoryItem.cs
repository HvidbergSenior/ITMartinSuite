namespace ITMartinLibrary.Domain.Entities;

public class InventoryItem
{
    public int Id { get; set; }

    public string Barcode { get; set; } = "";
    public int Quantity { get; set; }

    public DateTime FirstScannedAt { get; set; }
    public DateTime LastScannedAt { get; set; }

    public string Title { get; set; } = "";
    public string Type { get; set; } = "";
    public string AuthorOrDirector { get; set; } = "";

    public string Genre { get; set; } = "";
    public string Runtime { get; set; } = "";
    public string ReleaseYear { get; set; } = "";
    public string Plot { get; set; } = "";
    public string CoverUrl { get; set; } = "";

    public decimal Price { get; set; }
    public string ShelfLocation { get; set; } = "";

    public string LookupStatus { get; set; } = "Pending";
    public DateTime? DetailsUpdatedAt { get; set; }
}