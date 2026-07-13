namespace ITMartinElPriser.Server.Services;

public sealed class PricedHour
{
    public DateTime TimeUtc { get; set; }
    public DateTime TimeDk { get; set; }
    public bool IsEstimated { get; set; }
    public double SpotKrPerKwh { get; set; }
    public double NettarifKrPerKwh { get; set; }
    public double ElafgiftKrPerKwh { get; set; }
    public double LeverandoertillaegKrPerKwh { get; set; }
    public double MomsKrPerKwh { get; set; }
    public double TotalKrPerKwh { get; set; }
}

public sealed class PricedCheapWindow
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public double AvgTotalKrPerKwh { get; set; }
    public bool IsEstimated { get; set; }
}

public static class PriceBreakdownCalculator
{
    // Reduced to the EU minimum by Finanslov 2026, valid 2026-2027 (skat.dk).
    public const double ElafgiftKrPerKwh = 0.008;
    private const double MomsRate = 0.25;

    public static PricedHour Compute(PricePoint point, PriceSettings settings)
    {
        var nettarifKr = GetNettarifOre(point.TimeDk, settings) / 100.0;
        var tillaegKr = GetTillaegOre(settings) / 100.0;

        var subtotal = point.PriceKrPerKwh + nettarifKr + ElafgiftKrPerKwh + tillaegKr;
        var total = subtotal * (1 + MomsRate);

        return new PricedHour
        {
            TimeUtc = point.TimeUtc,
            TimeDk = point.TimeDk,
            IsEstimated = point.IsEstimated,
            SpotKrPerKwh = point.PriceKrPerKwh,
            NettarifKrPerKwh = Math.Round(nettarifKr, 4),
            ElafgiftKrPerKwh = ElafgiftKrPerKwh,
            LeverandoertillaegKrPerKwh = Math.Round(tillaegKr, 4),
            MomsKrPerKwh = Math.Round(total - subtotal, 4),
            TotalKrPerKwh = Math.Round(total, 3),
        };
    }

    // Best contiguous window by the all-in total price, not just the raw spot
    // price - a time-differentiated nettarif can change which hour is really
    // cheapest once spidslast is factored in.
    public static PricedCheapWindow? FindCheapestWindow(List<PricedHour> hours, int windowHours)
    {
        var upcoming = hours.Where(h => h.TimeDk >= DateTime.Now.AddMinutes(-59)).ToList();
        if (upcoming.Count < windowHours) return null;

        PricedCheapWindow? best = null;

        for (var i = 0; i + windowHours <= upcoming.Count; i++)
        {
            var slice = upcoming.Skip(i).Take(windowHours).ToList();
            var avg = slice.Average(h => h.TotalKrPerKwh);

            if (best is null || avg < best.AvgTotalKrPerKwh)
            {
                best = new PricedCheapWindow
                {
                    Start = slice[0].TimeDk,
                    End = slice[^1].TimeDk.AddHours(1),
                    AvgTotalKrPerKwh = Math.Round(avg, 3),
                    IsEstimated = slice.Any(h => h.IsEstimated),
                };
            }
        }

        return best;
    }

    private static double GetTillaegOre(PriceSettings settings)
    {
        if (settings.SupplierId == "custom") return settings.CustomTillaegOre;

        var preset = SupplierPreset.All.FirstOrDefault(s => s.Id == settings.SupplierId);
        return preset?.MarkupOreExVat ?? settings.CustomTillaegOre;
    }

    // Standard Forsyningstilsynet-aligned 3-band model: spidslast (17-21) only
    // applies on weekday evenings in the winter half of the year (Oct-Mar).
    // Weekends and the summer half (Apr-Sep) are simplified down to the "lav"
    // rate - real summer schedules are flatter but far less consistently
    // published than the winter ones, so this errs cheap/simple over guessing.
    private static double GetNettarifOre(DateTime timeDk, PriceSettings settings)
    {
        if (settings.GridCompanyId == "custom") return settings.CustomNettarifOre;

        var preset = GridCompanyPreset.All.FirstOrDefault(g => g.Id == settings.GridCompanyId);
        if (preset is null || preset.Id == "custom") return settings.CustomNettarifOre;

        var isWinter = timeDk.Month is >= 10 or <= 3;
        var isWeekday = timeDk.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

        if (!isWinter || !isWeekday) return preset.WinterLavOre;

        return timeDk.Hour switch
        {
            >= 17 and < 21 => preset.WinterSpidslastOre,
            >= 6 and < 17 or >= 21 => preset.WinterHojOre,
            _ => preset.WinterLavOre,
        };
    }
}
