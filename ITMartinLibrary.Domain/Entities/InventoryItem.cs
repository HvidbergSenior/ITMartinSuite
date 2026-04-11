namespace ITMartinLibrary.Domain.Entities;

public class InventoryItem
{
    public int Id { get; set; }

    public string Barcode { get; set; } = "";

    public int Quantity { get; set; }

    public DateTime FirstScannedAt { get; set; }

    public DateTime LastScannedAt { get; set; }

    // Optional fields to fill later
    public string Title { get; set; } = "";
    public string Type { get; set; } = "";
    public string AuthorOrDirector { get; set; } = "";
    public decimal Price { get; set; }
    public string ShelfLocation { get; set; } = "";
}