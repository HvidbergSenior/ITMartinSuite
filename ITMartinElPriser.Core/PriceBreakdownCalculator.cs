namespace ITMartinElPriser.Core;

public sealed class PricedPoint
{
    public DateTime TimeUtc { get; set; }
    public DateTime TimeDk { get; set; }
    public double SpotKrPerKwh { get; set; }
    public double NettarifKrPerKwh { get; set; }
    public double ElafgiftKrPerKwh { get; set; }
    public double EnerginetTarifKrPerKwh { get; set; }
    public double LeverandoertillaegKrPerKwh { get; set; }
    public double MomsKrPerKwh { get; set; }
    public double TotalKrPerKwh { get; set; }

    // What the household actually pays per kWh in this slot - all-in, or
    // just spot if they asked for that in settings.
    public double KrPerKwh(bool allIn) => allIn ? TotalKrPerKwh : SpotKrPerKwh;
}

public static class PriceBreakdownCalculator
{
    // Reduced to the EU minimum by Finanslov 2026, valid 2026-2027 (skat.dk).
    public const double ElafgiftKrPerKwh = 0.008;
    // Energinet transmissionsnettarif (4.30 øre) + systemtarif (7.20 øre), ex
    // moms, 2026 - the same for everyone regardless of supplier or grid
    // company. Read off an NRGi bill 2026-09 (5.38 + 9.00 øre inkl. moms).
    public const double EnerginetTarifKrPerKwh = 0.1150;
    private const double MomsRate = 0.25;

    public static PricedPoint Compute(PricePoint point, HouseholdSettings settings)
    {
        var nettarifKr = GetNettarifOre(point.TimeDk, settings) / 100.0;
        var tillaegKr = GetTillaegOre(settings) / 100.0;

        var subtotal = point.PriceKrPerKwh + nettarifKr + EnerginetTarifKrPerKwh + ElafgiftKrPerKwh + tillaegKr;
        var total = subtotal * (1 + MomsRate);

        return new PricedPoint
        {
            TimeUtc = point.TimeUtc,
            TimeDk = point.TimeDk,
            SpotKrPerKwh = point.PriceKrPerKwh,
            NettarifKrPerKwh = Math.Round(nettarifKr, 4),
            ElafgiftKrPerKwh = ElafgiftKrPerKwh,
            LeverandoertillaegKrPerKwh = Math.Round(tillaegKr, 4),
            EnerginetTarifKrPerKwh = EnerginetTarifKrPerKwh,
            MomsKrPerKwh = Math.Round(total - subtotal, 4),
            TotalKrPerKwh = Math.Round(total, 4),
        };
    }

    private static double GetTillaegOre(HouseholdSettings settings)
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
    private static double GetNettarifOre(DateTime timeDk, HouseholdSettings settings)
    {
        // The grid company's own published tariff, when the postcode has picked one.
        if (settings.NettarifLookup?.Invoke(timeDk) is { } exact) return exact * 100;

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
