namespace ITMartinElPriser.Core;

/// <summary>What one person tells us about their driving. kWh are from the socket (what you pay for).</summary>
public sealed class ChargeProfile
{
    public double KwhPerMonth { get; set; } = 230;
    /// <summary>Share of the kWh per place, in percent. Places with 0 are not used.</summary>
    public Dictionary<ChargeSpot, double> SharePct { get; set; } = new()
    {
        [ChargeSpot.Hjem] = 0, [ChargeSpot.Forening] = 90, [ChargeSpot.Arbejde] = 0, [ChargeSpot.Offentlig] = 5, [ChargeSpot.Lyn] = 5,
    };
    public bool HasHomeBox { get; set; }
    /// <summary>kr/kWh at home on the household's own electricity contract (from ElPriser's real prices).</summary>
    public double HomeKrPerKwh { get; set; } = 1.50;
    public string ForeningOperator { get; set; } = "egen";
    public double ForeningKrPerKwh { get; set; } = 2.38;
    /// <summary>What the person pays per kWh at work (0 when the employer pays).</summary>
    public double ArbejdeKrPerKwh { get; set; }
    public bool PlugInHybrid { get; set; }
}

public sealed record SpotCost(ChargeSpot Spot, double Kwh, double KrPerKwh, double Kr, string PaidVia);

public sealed record ChargeOption(
    string Id, string Title, string Company, double MonthlyKr, double FeeKr, double OneOff,
    IReadOnlyList<SpotCost> Lines, ChargeProduct? Product, string? Problem)
{
    public double KrPerKwh => Lines.Sum(l => l.Kwh) is var k and > 0 ? MonthlyKr / k : 0;
}

/// <summary>
/// Prices one person's month for every product: the product's fee + its own prices where it applies, and the
/// cheapest pay-as-you-go price everywhere else. "Uden abonnement" is the baseline everyone can compare with.
/// </summary>
public static class ChargeComparer
{
    public static double Kwh(ChargeProfile p, ChargeSpot s) =>
        p.KwhPerMonth * Math.Max(0, p.SharePct.GetValueOrDefault(s)) / Math.Max(1, p.SharePct.Values.Sum());

    public static PublicPrice CheapestPublic(ChargeSpot s) =>
        ChargeCatalog.PayAsYouGo.Where(x => x.Spot == s).MinBy(x => x.KrPerKwh)!;

    public static List<ChargeOption> Compare(ChargeProfile p, IEnumerable<ChargeProduct>? products = null)
    {
        var all = new List<ChargeOption> { Price(p, null) };
        all.AddRange((products ?? ChargeCatalog.Products).Select(x => Price(p, x)));
        return all.OrderBy(o => o.Problem is null ? 0 : 1).ThenBy(o => o.MonthlyKr).ToList();
    }

    /// <summary>Baseline price for a place without any subscription (null = the place is not available).</summary>
    private static (double Kr, string Via)? Baseline(ChargeProfile p, ChargeSpot s) => s switch
    {
        ChargeSpot.Hjem => p.HasHomeBox ? (p.HomeKrPerKwh, "din egen boks + din elaftale") : null,
        ChargeSpot.Forening => (p.ForeningKrPerKwh, "foreningens pris"),
        ChargeSpot.Arbejde => (p.ArbejdeKrPerKwh, p.ArbejdeKrPerKwh == 0 ? "betalt af arbejdsgiver" : "din andel på arbejdet"),
        _ => CheapestPublic(s) is var c ? (c.KrPerKwh, $"{c.Network} uden abonnement") : null,
    };

    private static ChargeOption Price(ChargeProfile p, ChargeProduct? x)
    {
        var lines = new List<SpotCost>();
        string? problem = null;
        var included = x?.IncludedKwh ?? 0;

        foreach (var s in Enum.GetValues<ChargeSpot>())
        {
            var kwh = Kwh(p, s);
            if (kwh <= 0.01) continue;

            double? own = null;
            if (x is not null)
            {
                if (x.Prices.TryGetValue(s, out var v)) own = v;
                else if (s == ChargeSpot.Forening && x.CoversForeningIfOperator == p.ForeningOperator) own = 0;
                if (own is not null && s == ChargeSpot.Hjem && x.HomeAtOwnPrice) own = p.HomeKrPerKwh;
            }

            if (own is double price)
            {
                // Included kWh are used first, then the product's own price (+ surcharge on free charging).
                var free = Math.Min(included, kwh);
                included -= free;
                var paidKwh = kwh - free;
                var krPerKwh = price + (price == 0 ? x!.SurchargePerKwh : 0);
                var kr = paidKwh * krPerKwh;
                lines.Add(new(s, kwh, kwh > 0 ? kr / kwh : 0, kr, x!.Name + (free > 0 ? $" ({free:0} kWh inkl.)" : "")));
                continue;
            }

            var b = Baseline(p, s);
            if (b is null)
            {
                problem = s == ChargeSpot.Hjem ? "kræver en ladeboks derhjemme" : $"ingen pris for {s}";
                continue;
            }
            lines.Add(new(s, kwh, b.Value.Kr, kwh * b.Value.Kr, b.Value.Via));
        }

        var fee = x is null ? 0 : (p.PlugInHybrid && x.HybridMonthlyFee is double h ? h : x.MonthlyFee);
        var total = fee + lines.Sum(l => l.Kr);
        return x is null
            ? new("ingen", "Uden abonnement", "", total, 0, 0, lines, null, problem)
            : new(x.Id, x.Name, x.Company, total, fee, x.OneOff, lines, x, problem);
    }

    public static string SpotName(ChargeSpot s) => s switch
    {
        ChargeSpot.Hjem => "Hjemme",
        ChargeSpot.Forening => "Foreningens lader",
        ChargeSpot.Arbejde => "På arbejdet",
        ChargeSpot.Offentlig => "Offentlig lader",
        ChargeSpot.Lyn => "Lynlader",
        _ => s.ToString(),
    };
}
