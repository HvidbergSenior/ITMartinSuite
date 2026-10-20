namespace ITMartinLager.Domain;

// Day offers (user 2026-10-20: "Products of the day like books monday -> 50 pct on used books. Also Monday 10 for 800,
// tuesday 10 for 700 and so on. Or 31. in every month 10 for 300"). A campaign is a rule a person switches on in Lager;
// the price of a basket is worked out here, so the shop, the basket page and the payment always agree.

public static class CampaignKinds
{
    public const string Percent = "Procent", Bundle = "Bundt";
    public static readonly string[] All = [Percent, Bundle];
}

public sealed class Campaign
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Kind { get; set; } = CampaignKinds.Percent;
    /// <summary>ProductTypes (Bøger, Film ...); "" = everything.</summary>
    public string ProductType { get; set; } = "";
    /// <summary>Usages: "Brugt" = only used things, "Ny" = only new, "" = both (user 2026-10-20: "50 pct on used books").</summary>
    public string Usage { get; set; } = "";
    /// <summary>The weekday it runs (0 = Sunday ... 6 = Saturday), null = any weekday.</summary>
    public int? Weekday { get; set; }
    /// <summary>The day of the month it runs (31 = the 31st, only in months that have one), null = any day.</summary>
    public int? DayOfMonth { get; set; }
    public int Percent { get; set; }             // Procent: off the price
    public int Count { get; set; }               // Bundt: this many things ...
    public int BundleKr { get; set; }            // ... for this many kroner
    public bool Active { get; set; }

    public bool On(DateOnly day) => Active && Valid
        && (Weekday is null || (int)day.DayOfWeek == Weekday) && (DayOfMonth is null || day.Day == DayOfMonth);

    public bool Fits(string kind, string usage) =>
        (ProductType == "" || ProductTypes.For(kind) == ProductType) && (Usage == "" || Usage == Usages.Normalise(usage));

    public bool Valid => Kind == CampaignKinds.Percent ? Percent is > 0 and < 100 : Count >= 2 && BundleKr > 0;

    // "bøger", "brugte bøger", "alt nyt" ...
    private string What => (ProductType, Usage) switch
    {
        ("", "") => "alt",
        ("", var u) => u == Usages.New ? "alt nyt" : "alt brugt",
        (var t, "") => t.ToLowerInvariant(),
        (var t, var u) => (u == Usages.New ? "nye " : "brugte ") + t.ToLowerInvariant(),
    };

    /// <summary>"Bøger: 50 % i dag" / "10 for 800 kr" - what the buyer sees.</summary>
    public string Text => Kind == CampaignKinds.Percent
        ? $"{Percent} % på {What}"
        : $"{Count} for {BundleKr} kr" + (What == "alt" ? "" : $" ({What})");

    /// <summary>"Hver mandag" / "Den 31. i måneden" - when it runs.</summary>
    public string When => (Weekday, DayOfMonth) switch
    {
        (null, null) => "Hver dag",
        ({ } w, null) => "Hver " + DayName(w),
        (null, { } d) => $"Den {d}. i måneden",
        ({ } w, { } d) => $"Den {d}., når det er en {DayName(w)}",
    };

    public static string DayName(int weekday) => weekday switch
    {
        0 => "søndag", 1 => "mandag", 2 => "tirsdag", 3 => "onsdag", 4 => "torsdag", 5 => "fredag", _ => "lørdag",
    };

    /// <summary>The user's own examples, switched OFF until a person turns them on (prices are money).</summary>
    public static List<Campaign> Examples() =>
    [
        new() { Name = "Bogmandag", Kind = CampaignKinds.Percent, ProductType = ProductTypes.Boeger, Usage = Usages.Used, Weekday = 1, Percent = 50 },
        new() { Name = "Mandag 10 for 800", Kind = CampaignKinds.Bundle, Weekday = 1, Count = 10, BundleKr = 800 },
        new() { Name = "Tirsdag 10 for 700", Kind = CampaignKinds.Bundle, Weekday = 2, Count = 10, BundleKr = 700 },
        new() { Name = "Onsdag 10 for 600", Kind = CampaignKinds.Bundle, Weekday = 3, Count = 10, BundleKr = 600 },
        new() { Name = "Torsdag 10 for 500", Kind = CampaignKinds.Bundle, Weekday = 4, Count = 10, BundleKr = 500 },
        new() { Name = "Fredag 10 for 400", Kind = CampaignKinds.Bundle, Weekday = 5, Count = 10, BundleKr = 400 },
        new() { Name = "Den 31.: 10 for 300", Kind = CampaignKinds.Bundle, DayOfMonth = 31, Count = 10, BundleKr = 300 },
    ];
}

/// <summary>One copy in a basket, at its normal shop price.</summary>
public sealed record BasketLine(int ItemId, string Kind, int Kr, string Usage = Usages.Used);

/// <summary>A copy with the price it is sold for today, and why (the campaign's text, or "").</summary>
public sealed record PricedLine(int ItemId, int ListKr, int Kr, string Offer);

public sealed record PricedBasket(List<PricedLine> Lines)
{
    public int Total => Lines.Sum(l => l.Kr);
    public int Saved => Lines.Sum(l => l.ListKr - l.Kr);
}

public static class Campaigns
{
    /// <summary>Today's price for a basket. A copy gets at most one offer, and the buyer always gets the better deal:
    /// bundles first, cheapest per thing first, each filled with the dearest copies (that saves the most) and only when
    /// the bundle is cheaper than paying for them one by one; then the best percentage on what is left.</summary>
    public static PricedBasket Price(IReadOnlyList<BasketLine> basket, IEnumerable<Campaign> campaigns, DateOnly day)
    {
        var today = campaigns.Where(c => c.On(day)).ToList();
        var left = basket.ToList();
        var priced = new List<PricedLine>();

        foreach (var b in today.Where(c => c.Kind == CampaignKinds.Bundle).OrderBy(c => (double)c.BundleKr / c.Count))
        {
            while (true)
            {
                var group = left.Where(l => b.Fits(l.Kind, l.Usage)).OrderByDescending(l => l.Kr).Take(b.Count).ToList();
                if (group.Count < b.Count || group.Sum(l => l.Kr) <= b.BundleKr) break;
                priced.AddRange(Share(group, b.BundleKr, b.Text));
                foreach (var l in group) left.Remove(l);
            }
        }

        foreach (var l in left)
        {
            var best = today.Where(c => c.Kind == CampaignKinds.Percent && c.Fits(l.Kind, l.Usage)).MaxBy(c => c.Percent);
            priced.Add(best is null
                ? new PricedLine(l.ItemId, l.Kr, l.Kr, "")
                : new PricedLine(l.ItemId, l.Kr, (int)Math.Round(l.Kr * (100 - best.Percent) / 100m, MidpointRounding.AwayFromZero), best.Text));
        }

        // Back in the basket's own order.
        var order = basket.Select((l, n) => (l, n)).ToList();
        return new PricedBasket(order.Select(o =>
        {
            var p = priced.First(x => x.ItemId == o.l.ItemId && x.ListKr == o.l.Kr);
            priced.Remove(p);
            return p;
        }).ToList());
    }

    /// <summary>The percentage price of one thing today (for the shop list); null = no percentage today.</summary>
    public static int? DayPrice(string kind, string usage, int kr, IEnumerable<Campaign> campaigns, DateOnly day) =>
        DayOffer(kind, usage, campaigns, day) is { } best
            ? (int)Math.Round(kr * (100 - best.Percent) / 100m, MidpointRounding.AwayFromZero)
            : null;

    /// <summary>The best percentage offer for one thing today, or null.</summary>
    public static Campaign? DayOffer(string kind, string usage, IEnumerable<Campaign> campaigns, DateOnly day) =>
        campaigns.Where(c => c.On(day) && c.Kind == CampaignKinds.Percent && c.Fits(kind, usage)).MaxBy(c => c.Percent);

    // The bundle price shared over its copies in proportion to their prices, whole kroner, the remainder on the dearest.
    private static IEnumerable<PricedLine> Share(List<BasketLine> group, int bundleKr, string text)
    {
        var sum = group.Sum(l => l.Kr);
        var shares = group.Select(l => (int)(bundleKr * (long)l.Kr / sum)).ToList();
        shares[0] += bundleKr - shares.Sum();
        return group.Select((l, n) => new PricedLine(l.ItemId, l.Kr, shares[n], text));
    }
}
