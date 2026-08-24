namespace ITMartinR6Assistant.Domain;

public class R6Map
{
    public string Name { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public bool IsRanked { get; set; }
    public List<BombSite> Sites { get; set; } = new();
}
