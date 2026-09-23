namespace ITMartinR6Assistant.Domain;

public class R6Map
{
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public bool IsRanked { get; set; }
    // Folder under Data/blueprints holding one Ubisoft floor plan per floor (B.jpg, 1F.jpg, 2F.jpg, 3F.jpg, Tag.jpg).
    public string BlueprintSlug { get; set; } = "";
    // Room callouts per floor, shown in the map viewer so a player can tell which room they are in.
    public List<RoomLabel> Rooms { get; set; } = new();
    public List<BombSite> Sites { get; set; } = new();
}
