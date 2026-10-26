namespace ITMartinLager.Domain;

/// <summary>What a blad costs NEW in the kiosk today (user 2026-10-26: "there should be a what does it cost as new...
/// Basserne right now and anders and blade"). No shop has an open price list for the current issue, and cover prices
/// change about once a year - so Martin types them in once on /kiosk.</summary>
public sealed class KioskPrice
{
    public int Id { get; set; }
    public string Series { get; set; } = "";
    public int Kr { get; set; }
    public string Note { get; set; } = "";       // where the price is from ("abonnement.dk, okt. 2026")
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class Kiosk
{
    // Filled in the first time the table is made: the one price found 2026-10-26 (løssalg "lige omkring 40 kr").
    public static readonly KioskPrice[] Start =
        [new() { Series = "Anders And & Co.", Kr = 40, Note = "løssalg ifølge abonnement.dk, okt. 2026" }];

    /// <summary>The kiosk price of the item's series: every word of the series name in the item's series (or title,
    /// when it has none). The most specific name wins - "Anders And Ekstra" before "Anders And & Co.".</summary>
    public static KioskPrice? For(Item item, IEnumerable<KioskPrice> prices)
    {
        var mine = ListingParse.Words(item.Series.Trim() != "" ? item.Series : item.Title).ToHashSet();
        if (mine.Count == 0) return null;
        return prices.Select(p => (p, words: ListingParse.Words(p.Series)))
            .Where(x => x.words.Count > 0 && x.words.All(mine.Contains))
            .OrderByDescending(x => x.words.Count).Select(x => x.p).FirstOrDefault();
    }
}
