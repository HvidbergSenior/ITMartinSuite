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

/// <summary>One thing to price: what it is, the suggested shop price (HintKr, 10 % under the sites), and what was found.</summary>
/// Usage = Brugt / Som ny / Ny; NewKr/NewShop = the lowest price at a Danish new shop (Bog & idé, Fantask) to compare with.
public sealed record VurderingItem(int Id, string Line, string Kind, string Condition, int? HintKr, int? SimilarKr, string Box,
    List<VurderingFind> Finds, string Usage = "Brugt", int? NewKr = null, string? NewShop = null);

/// <summary>How many wait in each pile, and the next thing(s).</summary>
public sealed record VurderingQueue(int Assess, int NoPrice, List<VurderingItem> Items);

/// <summary>The price a person set - Ok = the suggestion, or their own.</summary>
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
