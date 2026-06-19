namespace ITMartinLibrary.Domain.Entities;

public class ShelfBook
{
    public int Id { get; set; }
    public int ScannedShelfId { get; set; }
    public ScannedShelf Shelf { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public double BBoxX { get; set; }
    public double BBoxY { get; set; }
    public double BBoxW { get; set; }
    public double BBoxH { get; set; }
}
