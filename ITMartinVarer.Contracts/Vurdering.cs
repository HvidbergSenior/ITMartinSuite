namespace ITMartinVarer.Contracts;

// Vurdering (step 2, user 2026-11-05: "should finding prices be an app?" -> yes, its own helper): Lager keeps the things and
// hunts the prices; Vurdering shows them one at a time and a person says Ok or types a price. These are the shapes between them.

public static class VurderingHeaders
{
    /// <summary>Lager__VurderingKey there = Vurdering__LagerKey here (12+ characters).</summary>
    public const string Key = "X-Vurdering-Key";
}

/// <summary>Which pile: "vurderes" = found 50 kr or more, no own price yet; "ingen" = no price found anywhere.</summary>
public static class VurderingPiles
{
    public const string Assess = "vurderes", NoPrice = "ingen";
    public static bool Valid(string? pile) => pile is Assess or NoPrice;
}

/// <summary>A listing found on another site, shown so the person can judge.</summary>
public sealed record VurderingFind(string Source, string Name, int Kr, string Url, bool IsNew);

/// <summary>One thing to price: what it is, the suggested shop price (HintKr, Pricing.ShopPrice: 25 % under below 100 kr, else 10 %), and what was found.</summary>
/// Usage = Brugt / Som ny / Ny; NewKr/NewShop = the lowest price at a Danish new shop (Bog & idé, Fantask) to compare with.
public sealed record VurderingItem(int Id, string Line, string Kind, string Condition, int? HintKr, int? SimilarKr, string Box,
    List<VurderingFind> Finds, string Usage = "Brugt", int? NewKr = null, string? NewShop = null, int? StickerKr = null,
    // "Fundet på DBA 40 kr -> vores pris (halv pris) 20 kr" (2026-11-15): the market the suggestion came from, and the rule in words.
    int? MarketKr = null, string HintRule = "");

/// <summary>How many wait in each pile, and the next thing(s).</summary>
public sealed record VurderingQueue(int Assess, int NoPrice, List<VurderingItem> Items);

/// <summary>The price a person set - Ok = the suggestion, or their own.</summary>
/// <summary>One line of the gold list, shown in Vurdering (moved from Lager /guld, 2026-11-15). Badge = "🏆 GULD" etc.</summary>
public sealed record VurderingGold(string Tier, string Badge, string Type, string Category, string Issue, string Name, int Kr,
    int Listings, string Source, string Url, DateTime SeenAt);

/// <summary>What the newest issue of a blad costs new in the kiosk today (moved from Lager /kiosk, 2026-11-15).</summary>
public sealed record VurderingKiosk(int Id, string Series, int Kr, string Note, DateTime UpdatedAt);
public sealed record VurderingKioskSave(string Series, int Kr, string Note);

/// <summary>Answer to "Find priser nu" (2026-11-15): how many sites answered.</summary>
public sealed record VurderingHunted(int Sites);

public sealed record VurderingPrice(int Kr);

/// <summary>"Som ny" or "Brugt" set by the person in Vurdering.</summary>
public sealed record VurderingUsage(string Usage);

/// <summary>The usage filter on the queue: "" = all, "somny" = only Som ny, "brugt" = only Brugt.</summary>
public static class VurderingFilter
{
    public const string All = "", AsNew = "somny", Used = "brugt";
    public static string Valid(string? f) => f is AsNew or Used ? f : All;
    public static bool Shows(string filter, string usage) => filter switch
    {
        AsNew => Usages.Normalise(usage) == Usages.AsNew,
        Used => Usages.Normalise(usage) == Usages.Used,
        _ => true,
    };
}

// Descriptions for Tingshoppen and Strikkeshoppen (user 2026-11-10: "why no description?" + "i want to have ai look on the
// things"): Lager lists the Ting/Strik things with a picture and no text for buyers; Vurdering's dødsbo AI looks at them
// (several in one call, its daily cap) and sends back a title, the text and a price range. Lager decides what it keeps.

/// <summary>A thing waiting for a description. Kind = Ting or Strik; Title = what the person typed.</summary>
public sealed record VurderingDescribe(int Id, string Kind, string Title);

/// <summary>What the AI saw: a title, the text for buyers (the story), and a price range in kr (null = not sure).
/// Empty Title and Text = the AI gave no answer - Lager only notes that it was looked at.</summary>
public sealed record VurderingDescription(string Title, string Text, int? Low, int? High);
