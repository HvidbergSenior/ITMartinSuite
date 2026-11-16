namespace ITMartinLager.Domain;

/// <summary>Learn from our own sales (2026-11-16 price analysis, user "do all 4"): the only real truth about what sells at the
/// loppemarked is what sold. Per kind, once there are MinSales sales with the suggestion they had, the suggestion is moved by
/// the middle of sold / suggested (between MinFactor and MaxFactor, so one odd sale cannot run away with it). Before that: 1.</summary>
public static class SalesCalibration
{
    public const int MinSales = 50;
    public const decimal MinFactor = 0.5m, MaxFactor = 1.5m;

    public static decimal Factor(IEnumerable<(string Kind, int Hint, int Sold)> sales, string kind)
    {
        var ratios = sales.Where(s => s.Kind == kind && s.Hint > 0 && s.Sold > 0).Select(s => (decimal)s.Sold / s.Hint).OrderBy(r => r).ToList();
        if (ratios.Count < MinSales) return 1m;
        return Math.Clamp(ratios[(ratios.Count - 1) / 2], MinFactor, MaxFactor);
    }

    /// <summary>The suggestion moved by the factor, kept on the same 5-kr steps as Pricing.ShopPrice from 20 kr.</summary>
    public static int? Apply(int? hint, decimal factor)
    {
        if (hint is not > 0 || factor == 1m) return hint;
        var p = hint.Value * factor;
        return p < 20 ? (int)Math.Max(1, Math.Round(p)) : (int)(Math.Floor(p / 5) * 5);
    }
}
