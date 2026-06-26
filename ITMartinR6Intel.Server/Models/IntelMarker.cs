namespace ITMartinR6Intel.Server.Models;

public class IntelMarker
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "Enemy"; // Enemy, Gadget, Caution, Rotate, Player1, Player2, Player3, Player4, Player5, Bomb
    public double X { get; set; } // percentage 0-100 of image width
    public double Y { get; set; } // percentage 0-100 of image height
    public string Floor { get; set; } = "1F";
    public string? Note { get; set; }
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
}
