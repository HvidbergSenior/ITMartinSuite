namespace ITMartinLibrary.Domain.Entities;

public class ScannedShelf
{
    public int Id { get; set; }
    public int ShelfNumber { get; set; }
    public string ImagePath { get; set; } = "";
    public DateTime ScannedAt { get; set; }
    public List<ShelfBook> Books { get; set; } = [];
}
