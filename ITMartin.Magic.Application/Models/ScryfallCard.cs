namespace ITMartin.Magic.Application.Models;

public class ScryfallCard
{
    public string Name { get; set; } = "";

    public string Set { get; set; } = "";

    public string CollectorNumber { get; set; } = "";

    public string ManaCost { get; set; } = "";

    public string TypeLine { get; set; } = "";

    public string Rarity { get; set; } = "";

    public string OracleText { get; set; } = "";

    public string Artist { get; set; } = "";

    public string Frame { get; set; } = "";

    public string BorderColor { get; set; } = "";

    public string Power { get; set; } = "";

    public string Toughness { get; set; } = "";

    public string ImageUrl { get; set; } = "";

    public string ReleasedAt { get; set; } = "";

    public List<string> Finishes { get; set; } = [];

    public int MatchScore { get; set; }

    public decimal? EurPrice { get; set; }

    public decimal? EurFoilPrice { get; set; }

    public decimal? UsdPrice { get; set; }

    public decimal? UsdFoilPrice { get; set; }
}