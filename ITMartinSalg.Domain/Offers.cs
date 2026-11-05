namespace ITMartinSalg.Domain;

// The marketplace's two offers (user 2026-11-05: "sale should be built on liveauction and marketplace" + "user should be able
// to click on items and make batches"). Prices are hidden on bogshoppen.dk, so a customer says what they would pay.

/// <summary>"Byd" = the most this customer would pay for one thing - HIDDEN (proxy bid, eBay style, decided 2026-10-15).
/// The Saturday live auction bids for them up to this. One bid per customer per thing; a new one replaces it.</summary>
public sealed class Bid
{
    public const int LowestKr = 1, HighestKr = 100_000;

    public int Id { get; set; }
    public int ItemId { get; set; }
    public string Customer { get; set; } = "";   // kundenummer
    public int MaxKr { get; set; }
    public DateTime PlacedAt { get; set; }

    public static Bid Place(string? customer, int itemId, int maxKr, DateTime now)
    {
        if (maxKr is < LowestKr or > HighestKr) throw new InvalidOperationException($"Skriv et beløb mellem {LowestKr} og {HighestKr:N0} kr.");
        return new() { Customer = Kundenummer.Require(customer), ItemId = itemId, MaxKr = maxKr, PlacedAt = now };
    }
}

/// <summary>"Mit bundt": several things together - one bid for all of them, or a question "what would you take?".
/// Martin answers ("den er jeres for X kr"); nothing is sold until he says yes in Salg.</summary>
public sealed class BundleOffer
{
    public const int MinItems = 2, MaxItems = 50;
    public const string New = "Ny", Answered = "Besvaret";

    public int Id { get; set; }
    public string Customer { get; set; } = "";
    /// <summary>The things, as "12,40,77" - in the order the customer chose them.</summary>
    public string ItemIds { get; set; } = "";
    /// <summary>Their bid for all of them; null = they ask for a price.</summary>
    public int? Kr { get; set; }
    public string Status { get; set; } = New;
    public DateTime CreatedAt { get; set; }

    public IReadOnlyList<int> Ids => ItemIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    public bool AsksForPrice => Kr is null;

    public static BundleOffer Make(string? customer, IEnumerable<int> itemIds, int? kr, DateTime now)
    {
        var ids = itemIds.Where(i => i > 0).Distinct().ToList();
        if (ids.Count < MinItems) throw new InvalidOperationException($"Et bundt er mindst {MinItems} ting.");
        if (ids.Count > MaxItems) throw new InvalidOperationException($"Et bundt er højst {MaxItems} ting.");
        if (kr is < Bid.LowestKr or > Bid.HighestKr) throw new InvalidOperationException($"Skriv et beløb mellem {Bid.LowestKr} og {Bid.HighestKr:N0} kr – eller spørg om en pris.");
        return new() { Customer = Kundenummer.Require(customer), ItemIds = string.Join(',', ids), Kr = kr, CreatedAt = now };
    }

    public void MarkAnswered() => Status = Answered;
}
