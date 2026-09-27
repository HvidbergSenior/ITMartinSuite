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
    // Bans the statistics say are mandatory (official ban rate >= 30 %), shown first and marked.
    public List<string> MustBans { get; set; } = new();
    // Operator name -> why he is banned on THIS site.
    public Dictionary<string, string> BanReasons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Short numbered facts about exactly this site (entry routes, hatches, a plan, the usual defence).
    public List<string> QuickInfo { get; set; } = new();
    // 10 suggested picks per side for this site (computed with the bans) + why.
    public List<string> PickSuggestions { get; set; } = new();
    public Dictionary<string, string> PickReasons { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Site position on its floor blueprint, in percent of the image (set by clicking in the editor).
    public double? MarkerX { get; set; }
    public double? MarkerY { get; set; }
    public List<BattlePlan> BattlePlans { get; set; } = new();
    public string Note { get; set; } = "";
}
