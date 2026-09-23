namespace ITMartinR6Assistant.Domain;

// A room callout on a map floor, placed on the floor blueprint in percent of the image.
public class RoomLabel
{
    public string Name { get; set; } = "";
    public string Floor { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
}
