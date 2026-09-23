namespace ITMartinR6Assistant.Domain;

// Pro-league numbers computed from Liquipedia's match records (S-tier events, last two years, recent events
// weighted higher). Liquipedia stores bans, map vetoes and rounds per side - not operator picks or bombsites.
public class ProStats
{
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public List<string> Events { get; set; } = new();
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Matches { get; set; }
    public int MapsPlayed { get; set; }
    // Weighted percent of played pro maps where the operator was banned (by either team).
    public Dictionary<string, double> OperatorBanPct { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Percent of maps where the operator was one of the team's first two bans - what pros fear most.
    public Dictionary<string, double> EarlyBanPct { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ProMapStats> Maps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ProMapStats
{
    public int Played { get; set; }
    public int Vetoes { get; set; }
    public double PickPct { get; set; }       // picked (or left as decider) in this share of map vetoes
    public double BanPct { get; set; }        // banned in this share of map vetoes
    public int Rounds { get; set; }
    public double AtkRoundPct { get; set; }   // regulation rounds won by the attacking team
    public double DefStartWinPct { get; set; } // maps won by the team that started on defence
    public Dictionary<string, double> OperatorBanPct { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ProMatchRef> RecentMatches { get; set; } = new();
}

public class ProMatchRef
{
    public DateOnly Date { get; set; }
    public string Event { get; set; } = "";
    public string Teams { get; set; } = "";
    public string Score { get; set; } = "";
    public string Url { get; set; } = "";
}
