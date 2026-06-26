namespace ITMartinR6Assistant.Domain;

public class BombSite
{
    public string Name { get; set; } = "";
    public string Floor { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public int AttackWinRate { get; set; }
    public List<string> AttackPicks { get; set; } = new();
    public List<string> DefensePicks { get; set; } = new();
    public List<string> SuggestedBans { get; set; } = new();
    public List<BattlePlan> BattlePlans { get; set; } = new();
    public string Note { get; set; } = "";
}
