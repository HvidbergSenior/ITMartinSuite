namespace ITMartinVurdering.Domain;

/// <summary>
/// The person's price (user 2026-11-05: one thing at a time, Ok or your own price). People type "120", "120 kr",
/// "120,-" or "1.200" - all mean whole kroner. 1 to 100.000 kr.
/// </summary>
public static class PriceRules
{
    public const int Min = 1, Max = 100_000;

    /// <summary>Whole kroner from what was typed, or null when it is not a price.</summary>
    public static int? Parse(string? typed)
    {
        var t = (typed ?? "").Trim().ToLowerInvariant().Replace("kr", "").Replace(",-", "").Replace(".", "").Replace(" ", "");
        if (t.Contains(',')) t = t[..t.IndexOf(',')];   // "49,50" -> 49: shop prices are whole kroner
        return int.TryParse(t, out var kr) && kr is >= Min and <= Max ? kr : null;
    }

    /// <summary>Ok = take the suggestion; only when there is one.</summary>
    public static int? Ok(int? hintKr) => hintKr is >= Min and <= Max ? hintKr : null;
}
