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
    // Haggling (user 2026-11-21: "both buyer and seller can say all these for this price"): Ny = Martin's turn, Modbud = the
    // customer's turn (Martin said a price, or offered the bundle himself), Aftalt / Afvist = done. Besvaret = answered at once
    // by the shop (AnswerKr) or marked by hand - the customer can still say yes or give a new bid.
    public const string New = "Ny", Answered = "Besvaret", Counter = "Modbud", Agreed = "Aftalt", Rejected = "Afvist";

    public int Id { get; set; }
    public string Customer { get; set; } = "";
    /// <summary>The things, as "12,40,77" - in the order the customer chose them.</summary>
    public string ItemIds { get; set; } = "";
    /// <summary>Their bid for all of them; null = they ask for a price.</summary>
    public int? Kr { get; set; }
    public string Status { get; set; } = New;
    /// <summary>The answer given at once to "Spørg om en pris" (2026-11-16): the price for all of it, and the random extra %.</summary>
    public int? AnswerKr { get; set; }
    public int? ExtraPercent { get; set; }
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

    public void MarkAnswered() { Status = Answered; }

    /// <summary>Answered by the shop itself: the price is the customer's to say yes to (a bid at that price).</summary>
    public void AnswerAutomatically(int kr, int extraPercent)
    {
        AnswerKr = kr; ExtraPercent = extraPercent; Status = Answered;
    }

    /// <summary>"Dit bud" on one thing (2026-11-21, user: "found on Faraos -> link, your offer"): a bundle of one, so it is
    /// haggled the same way as Mit bundt.</summary>
    public static BundleOffer Single(string? customer, int itemId, int kr, DateTime now)
    {
        if (itemId <= 0) throw new InvalidOperationException("Varen findes ikke.");
        if (kr is < Bid.LowestKr or > Bid.HighestKr) throw new InvalidOperationException($"Skriv et beløb mellem {Bid.LowestKr} og {Bid.HighestKr:N0} kr.");
        return new() { Customer = Kundenummer.Require(customer), ItemIds = itemId.ToString(), Kr = kr, CreatedAt = now, UpdatedAt = now };
    }

    /// <summary>Martin started it: "alle disse for X kr" to one customer.</summary>
    public bool FromShop { get; set; }
    /// <summary>Martin's latest price (his counter, or his own offer).</summary>
    public int? ShopKr { get; set; }
    /// <summary>The price both said yes to.</summary>
    public int? AgreedKr { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsDone => Status is Agreed or Rejected;
    public bool ShopsTurn => Status == New;
    /// <summary>The price the customer can say yes to now, or null when it is not their turn.</summary>
    public int? PriceForCustomer => Status == Counter ? ShopKr : Status == Answered ? AnswerKr : null;

    public static BundleOffer FromShopOffer(string? customer, IEnumerable<int> itemIds, int kr, DateTime now)
    {
        var b = Make(customer, itemIds, null, now);
        if (kr is < Bid.LowestKr or > Bid.HighestKr) throw new InvalidOperationException($"Skriv et beløb mellem {Bid.LowestKr} og {Bid.HighestKr:N0} kr.");
        (b.FromShop, b.ShopKr, b.Status, b.UpdatedAt) = (true, kr, Counter, now);
        return b;
    }

    /// <summary>Martin's price back to the customer.</summary>
    public void ShopCounters(int kr, DateTime now)
    {
        if (IsDone) throw new InvalidOperationException("Bundtet er afsluttet.");
        if (kr is < Bid.LowestKr or > Bid.HighestKr) throw new InvalidOperationException($"Skriv et beløb mellem {Bid.LowestKr} og {Bid.HighestKr:N0} kr.");
        (ShopKr, Status, UpdatedAt) = (kr, Counter, now);
    }

    /// <summary>Martin says yes to the customer's bid.</summary>
    public void ShopAccepts(DateTime now)
    {
        if (Status != New || Kr is not { } kr) throw new InvalidOperationException("Der er intet bud at sige ja til.");
        (AgreedKr, Status, UpdatedAt) = (kr, Agreed, now);
    }

    /// <summary>The customer's new bid after Martin's price.</summary>
    public void CustomerCounters(int kr, DateTime now)
    {
        if (IsDone) throw new InvalidOperationException("Bundtet er afsluttet.");
        if (PriceForCustomer is null) throw new InvalidOperationException("Vent på svar fra os.");
        if (kr is < Bid.LowestKr or > Bid.HighestKr) throw new InvalidOperationException($"Skriv et beløb mellem {Bid.LowestKr} og {Bid.HighestKr:N0} kr.");
        (Kr, Status, UpdatedAt) = (kr, New, now);
    }

    /// <summary>The customer says yes to Martin's price.</summary>
    public void CustomerAccepts(DateTime now)
    {
        if (PriceForCustomer is not { } kr) throw new InvalidOperationException("Der er ingen pris at sige ja til endnu.");
        (AgreedKr, Status, UpdatedAt) = (kr, Agreed, now);
    }

    /// <summary>Either side says no thanks.</summary>
    public void Reject(DateTime now)
    {
        if (IsDone) throw new InvalidOperationException("Bundtet er afsluttet.");
        (Status, UpdatedAt) = (Rejected, now);
    }
}
