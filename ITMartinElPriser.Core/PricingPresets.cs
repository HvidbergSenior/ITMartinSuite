namespace ITMartinElPriser.Core;

// Nettarif presets, sourced July 2026. Most Danish grid companies use the
// Forsyningstilsynet-aligned 3-band winter model (lav/høj/spidslast); Radius
// currently publishes a single blended average instead, so all three bands
// are set equal for them.
public sealed class GridCompanyPreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Region { get; init; } = "";
    public double WinterLavOre { get; init; }
    public double WinterHojOre { get; init; }
    public double WinterSpidslastOre { get; init; }
    public double MonthlySubscriptionKr { get; init; }

    public static readonly GridCompanyPreset Custom = new()
    {
        Id = "custom",
        Name = "Andet (indtast selv)",
        Region = "",
    };

    public static readonly IReadOnlyList<GridCompanyPreset> All =
    [
        new()
        {
            Id = "radius", Name = "Radius Elnet", Region = "København og Nordsjælland",
            WinterLavOre = 23.56, WinterHojOre = 23.56, WinterSpidslastOre = 23.56, MonthlySubscriptionKr = 51.04,
        },
        new()
        {
            // Lav = 13.43 øre inkl. moms on a Sept 2026 NRGi/N1 bill (10.74 ex). Winter
            // høj/spids use the standard 1:3:9 band ratios until a winter bill confirms
            // the real figures. Forbrugsabonnement 46.15 kr/md inkl. moms.
            // Named "N1" until 2026-09-28, but the bill it came from says Konstant Net A/S (Aarhus).
            // Id stays "n1" so settings already saved in phones' cookies keep working.
            Id = "n1", Name = "Konstant", Region = "Aarhus og Østjylland",
            WinterLavOre = 10.74, WinterHojOre = 32.2, WinterSpidslastOre = 96.7, MonthlySubscriptionKr = 36.92,
        },
        new()
        {
            Id = "cerius", Name = "Cerius", Region = "Sjælland (udenfor København)",
            WinterLavOre = 13, WinterHojOre = 40, WinterSpidslastOre = 120, MonthlySubscriptionKr = 0,
        },
        new()
        {
            Id = "trefor", Name = "Trefor El-net", Region = "Vejle / Fredericia / Kolding",
            WinterLavOre = 8, WinterHojOre = 24, WinterSpidslastOre = 73, MonthlySubscriptionKr = 0,
        },
        new()
        {
            Id = "trefor-oest", Name = "Trefor El-Net Øst (BEOF)", Region = "Bornholm",
            WinterLavOre = 15, WinterHojOre = 44, WinterSpidslastOre = 131, MonthlySubscriptionKr = 0,
        },
        Custom,
    ];
}

// Supplier ("leverandør") presets - the markup on top of the raw spot price.
// Figures are ex-VAT; moms is applied once, on the whole bill, in
// PriceBreakdownCalculator.
public sealed class SupplierPreset
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double MarkupOreExVat { get; init; }
    public double MonthlySubscriptionKr { get; init; }

    public static readonly SupplierPreset Custom = new()
    {
        Id = "custom",
        Name = "Andet (indtast selv)",
    };

    public static readonly IReadOnlyList<SupplierPreset> All =
    [
        // NRGi Time: 9 øre/kWh inkl. moms tillæg, 29 kr/md (Sept 2026 aftale page).
        new() { Id = "nrgi-time", Name = "NRGi Time", MarkupOreExVat = 7.2, MonthlySubscriptionKr = 23.2 },
        new() { Id = "norlys-flexel", Name = "Norlys FlexEl", MarkupOreExVat = 9.70, MonthlySubscriptionKr = 29 },
        new() { Id = "andel-flexenergi", Name = "Andel Energi FlexEnergi", MarkupOreExVat = 11.66, MonthlySubscriptionKr = 20 },
        new() { Id = "vindstoed-danskvind", Name = "Vindstød DanskVind", MarkupOreExVat = 0.5, MonthlySubscriptionKr = 0 },
        Custom,
    ];
}
