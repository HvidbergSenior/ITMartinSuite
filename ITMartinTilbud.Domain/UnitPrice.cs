namespace ITMartinTilbud.Domain;

/// <summary>Price per kg, litre or piece from a pack's size - the number that makes offers comparable.</summary>
public static class UnitPrice
{
    /// <param name="pieces">Packs in the offer (a 3-pack = 3).</param>
    /// <param name="from">Size of one pack (lower bound of a range).</param>
    /// <param name="to">Upper bound of a range ("400-500 g"), or the same as from.</param>
    /// <param name="siSymbol">"kg" or "l" when the size converts to those.</param>
    /// <param name="factor">Multiplier to kg/l (grams = 0.001).</param>
    /// <param name="unitSymbol">The unit as printed ("g", "ml", "pcs").</param>
    public static (decimal? Price, string? Label, string? Size) Compute(
        decimal price, decimal? pieces, decimal? from, decimal? to, string? siSymbol, decimal? factor, string? unitSymbol)
    {
        var n = pieces is > 0 ? pieces.Value : 1m;
        var unit = unitSymbol is "pcs" ? "stk" : unitSymbol;
        string? size = from is > 0 ? $"{(n > 1 ? $"{n:0} × " : "")}{from.Value:0.###}{(to is { } t && t != from ? $"–{t:0.###}" : "")} {unit}" : null;

        if (from is > 0 && siSymbol is "kg" or "l")
        {
            // A range ("400-500 g") is priced on the larger size: the honest lower bound of what you pay per kg.
            var amount = (to is > 0 ? Math.Max(from.Value, to.Value) : from.Value) * (factor ?? 1m) * n;
            if (amount > 0) return (Math.Round(price / amount, 2), siSymbol == "kg" ? "kr/kg" : "kr/l", size);
        }
        if (unit is "stk" && from is > 0)
            return (Math.Round(price / (from.Value * n), 2), "kr/stk", size);
        return (null, null, size);
    }
}
