namespace ITMartinElPriser.Core;

// One line as it appears on the bill, with the plain-Danish story behind it:
// who gets the money, what it pays for and whether the household can do
// anything about it (move usage in time, use less, or nothing at all).
public sealed record BillLine(
    string Id, string Title, string Recipient, double Kr, bool IsFixed,
    string What, string Influence, string Tip);

public sealed class Bill
{
    public required DateOnly From { get; init; }
    public required DateOnly To { get; init; }          // exclusive
    public required int DaysInPeriod { get; init; }
    public required int DaysMeasured { get; init; }
    public required double Kwh { get; init; }
    public required List<BillLine> Lines { get; init; }
    public double TotalKr => Lines.Sum(l => l.Kr);
    public bool IsComplete => DaysMeasured >= DaysInPeriod;

    // What the whole month will land on if the missing days look like the
    // measured ones: fixed fees stay, usage lines scale, moms follows.
    public double ProjectedKr
    {
        get
        {
            var fixedEx = Lines.Where(l => l.IsFixed).Sum(l => l.Kr);
            var variableEx = Lines.Where(l => !l.IsFixed && l.Id != "moms").Sum(l => l.Kr);
            var scale = DaysMeasured == 0 ? 0 : (double)DaysInPeriod / DaysMeasured;
            return (fixedEx + variableEx * scale) * 1.25;
        }
    }
    public double ProjectedKwh => DaysMeasured == 0 ? 0 : Kwh * DaysInPeriod / DaysMeasured;
}

public static class BillCalculator
{
    private const double Moms = 0.25;

    // kwhByHour: "yyyy-MM-dd HH" -> kWh for the days that have readings.
    // priced: every priced 15-min slot covering those days.
    public static Bill Build(DateOnly from, DateOnly to, IReadOnlyDictionary<DateTime, double> kwhByHour, IReadOnlyList<PricedPoint> priced, HouseholdSettings s)
    {
        var priceByHour = priced.GroupBy(p => new DateTime(p.TimeDk.Year, p.TimeDk.Month, p.TimeDk.Day, p.TimeDk.Hour, 0, 0))
            .ToDictionary(g => g.Key, g => g.ToList());

        double spot = 0, tillaeg = 0, net = 0, energinet = 0, afgift = 0, kwh = 0;
        var daysMeasured = new HashSet<DateOnly>();
        foreach (var (hour, k) in kwhByHour)
        {
            var d = DateOnly.FromDateTime(hour);
            if (d < from || d >= to) continue;
            if (!priceByHour.TryGetValue(hour, out var slots)) continue;
            daysMeasured.Add(d);
            kwh += k;
            spot += k * slots.Average(p => p.SpotKrPerKwh);
            tillaeg += k * slots.Average(p => p.LeverandoertillaegKrPerKwh);
            net += k * slots.Average(p => p.NettarifKrPerKwh);
            energinet += k * slots.Average(p => p.EnerginetTarifKrPerKwh);
            afgift += k * slots.Average(p => p.ElafgiftKrPerKwh);
        }

        var grid = GridCompanyPreset.All.FirstOrDefault(g => g.Id == s.GridCompanyId);
        var supplier = SupplierPreset.All.FirstOrDefault(p => p.Id == s.SupplierId);
        var gridAbo = grid?.MonthlySubscriptionKr ?? 0;
        var supplierAbo = supplier?.MonthlySubscriptionKr ?? 0;
        var gridName = grid is null || grid.Id == "custom" ? "netselskabet" : grid.Name;
        var supplierName = supplier is null || supplier.Id == "custom" ? "elleverandøren" : supplier.Name;

        var exMoms = spot + tillaeg + supplierAbo + net + gridAbo + energinet + afgift;

        var lines = new List<BillLine>
        {
            new("spot", "Strøm (spotpris)", supplierName, spot, false,
                "Selve strømmen, købt time for time på den nordiske elbørs Nord Pool. Prisen sættes dagen før kl. 13 ud fra vind, sol, vand i de norske magasiner og hvad Tyskland betaler. Din leverandør sender regningen videre uden fortjeneste her.",
                "Ja – både hvornår og hvor meget. Det er den post appens forside handler om.",
                "Kør maskinerne når søjlerne er grønne. Forskellen mellem billigste og dyreste time er ofte 1–2 kr/kWh."),
            new("tillaeg", "Leverandørtillæg", supplierName, tillaeg, false,
                "Det leverandøren tjener pr. kWh oven i spotprisen. Det er her elselskaberne konkurrerer – alt andet på regningen er ens uanset hvem du vælger.",
                "Kun ved at skifte leverandør eller aftale.",
                "Sammenlign tillæg + abonnement på elpris.dk. Alt andet er ens."),
            new("abo-lev", "Abonnement, elleverandør", supplierName, supplierAbo, true,
                "Fast beløb pr. måned for at have en aftale – uanset forbrug.",
                "Kun ved at skifte leverandør.",
                ""),
            new("net", "Nettarif (transport)", gridName, net, false,
                "Betaling for at få strømmen ud gennem de lokale kabler og transformere til huset. Netselskabet er et monopol i dit område – du kan ikke vælge et andet. Om vinteren (okt–mar) er tariffen op til 9 gange højere kl. 17–21 på hverdage end om natten.",
                "Ja – hvornår. Især om vinteren.",
                "Undgå 17–21 på vinterhverdage til tumbler, opvask og elbil."),
            new("abo-net", "Abonnement, netselskab", gridName, gridAbo, true,
                "Fast beløb pr. måned for at være tilsluttet elnettet og have en måler.",
                "Nej.",
                ""),
            new("energinet", "Energinet: transmission + system", "Energinet (staten)", energinet, false,
                "Det store landsdækkende højspændingsnet, udlandsforbindelserne og det at holde nettet i balance sekund for sekund. Samme sats for alle i Danmark.",
                "Kun ved at bruge mindre.",
                ""),
            new("afgift", "Elafgift", "Staten", afgift, false,
                "Statens afgift pr. kWh. Sat ned til EU's minimum (0,8 øre) fra 2026 – den var 76 øre i 2022. Husk det når du sammenligner med gamle regninger.",
                "Kun ved at bruge mindre.",
                ""),
            new("moms", "Moms 25 %", "Staten", exMoms * Moms, false,
                "25 % oven på alt det andet – også på afgifterne. Moms udgør derfor en femtedel af den samlede regning.",
                "Følger resten.",
                ""),
        };

        return new Bill
        {
            From = from, To = to,
            DaysInPeriod = to.DayNumber - from.DayNumber,
            DaysMeasured = daysMeasured.Count,
            Kwh = kwh,
            Lines = lines,
        };
    }
}
