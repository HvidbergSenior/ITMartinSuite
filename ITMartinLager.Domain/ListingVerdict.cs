namespace ITMartinLager.Domain;

// Stikprøve (user 2026-11-24: "if the listings are not precise or the rules are bad... How?" -> spot checks): Martin marks a found
// listing Rigtig or Forkert. A Forkert listing never counts for that thing's price again - kept by the listing's address, so a new
// price search that finds the same listing does not bring it back. The marks also show how good each source is.
public sealed class ListingVerdict
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string ListingKey { get; set; } = "";
    public string Source { get; set; } = "";
    public string Name { get; set; } = "";
    public int Kr { get; set; }
    public bool Right { get; set; }
    /// <summary>"Kan ikke se" (2026-11-24, user: CD backs can't be told apart): neither right nor wrong - it counts as before and stays out of the %.</summary>
    public bool CantSee { get; set; }
    public bool Wrong => !Right && !CantSee;
    public DateTime At { get; set; } = DateTime.UtcNow;

    /// <summary>The same listing every time: its address (eBay by item number), else source + name + kr.</summary>
    public static string KeyOf(PriceFind f) =>
        f.Url.Trim() != "" ? Pricing.ListingKey(f.Url) : $"{f.Source}|{f.Name.Trim().ToLowerInvariant()}|{f.Kr}";

    /// <summary>The finds without the ones Martin marked Forkert for this thing.</summary>
    public static List<PriceFind> WithoutWrong(IEnumerable<PriceFind> finds, IEnumerable<ListingVerdict> verdicts)
    {
        var wrong = verdicts.Where(v => v.Wrong).Select(v => (v.ItemId, v.ListingKey)).ToHashSet();
        return finds.Where(f => !wrong.Contains((f.ItemId, KeyOf(f)))).ToList();
    }

    /// <summary>Right / wrong per source, best first - how good the matches are.</summary>
    public static List<(string Source, int Right, int Wrong)> PerSource(IEnumerable<ListingVerdict> verdicts) =>
        verdicts.Where(v => !v.CantSee).GroupBy(v => v.Source)
            .Select(g => (g.Key, g.Count(v => v.Right), g.Count(v => v.Wrong)))
            .OrderByDescending(x => x.Item2 + x.Item3).ToList();
}
