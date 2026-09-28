using System.Globalization;
using System.Text.RegularExpressions;

namespace ITMartinElPriser.Core;

// Who a bill line goes to. Only the supplier part differs between electricity companies.
public enum BillParty { Supplier, Grid, State, Other }

public sealed record ElBillLine(BillParty Party, string Name, double? Kwh, double? RateKr, double Kr);

// A real electricity bill, read from the PDF the supplier sends. All amounts incl. VAT, as printed.
public sealed record ElBill(
    string Supplier, string Product, string Grid,
    DateOnly? From, DateOnly? To, DateOnly? InvoiceDate,
    double Kwh, double TotalKr, List<ElBillLine> Lines,
    string MeterNumber = "", string MeteringPointId = "", DateTime? UploadedUtc = null)
{
    public double KrPerKwh => Kwh > 0 ? TotalKr / Kwh : 0;
    public int Days => From is { } f && To is { } t ? t.DayNumber - f.DayNumber + 1 : 0;
    public double PartKr(BillParty party) => Lines.Where(l => l.Party == party).Sum(l => l.Kr);

    // The supplier's markup per kWh incl. VAT ("Pristillæg" / "Tillæg"), if the bill shows it.
    public double? MarkupKrPerKwh => Lines.FirstOrDefault(l => l.Party == BillParty.Supplier && l.RateKr is not null &&
        Regex.IsMatch(l.Name, "till[æa]g", RegexOptions.IgnoreCase))?.RateKr;

    // The supplier's monthly subscription incl. VAT, if the bill shows it.
    public double? SubscriptionKr => Lines.FirstOrDefault(l => l.Party == BillParty.Supplier && l.RateKr is null &&
        l.Name.Contains("abonnement", StringComparison.OrdinalIgnoreCase))?.Kr;

    // A year's use estimated from this bill's period.
    public double YearlyKwhEstimate => Days > 0 ? Kwh * 365.0 / Days : 0;
}

// The bill compared with what suppliers report to elpris.dk: is the markup what they say, and
// what would the cheapest similar product cost for this household's use? Used by the free
// ElPriser teaser and by MinElpris.
public sealed record BillComparison(
    SupplierProduct? Current, double? BillMarkupKr, double? PortalMarkupKr,
    double YearlyKwh, double? MarkupGapYearlyKr, SupplierProduct? Cheapest, double? SavingYearlyKr)
{
    public static BillComparison For(ElBill bill, IReadOnlyList<SupplierProduct> products)
    {
        var company = bill.Supplier.Split(' ')[0];
        var current = products.FirstOrDefault(p => p.Company.StartsWith(company, StringComparison.OrdinalIgnoreCase) &&
                                                   string.Equals(p.Name.Trim(), bill.Product.Trim(), StringComparison.OrdinalIgnoreCase));
        var kwh = bill.YearlyKwhEstimate > 0 ? bill.YearlyKwhEstimate : 3000;
        var portal = current is null ? (double?)null : current.SurchargeKrPerKwh * SupplierProduct.Vat;
        var gap = bill.MarkupKrPerKwh is { } m && portal is { } pm ? (m - pm) * kwh : (double?)null;

        // Same kind of price as today (variable vs fixed), fits this use, cheapest per year.
        var isFixed = current?.Fixed ?? false;
        var cheapest = products.Where(p => p.Fixed == isFixed && p.FitsFor(kwh)).OrderBy(p => p.YearlyKr(kwh)).FirstOrDefault();
        // What the household really pays the supplier today: the bill's own markup and subscription
        // when present (they can differ from the portal), otherwise the portal's figures.
        double? today = bill.MarkupKrPerKwh is { } bm && bill.SubscriptionKr is { } bs && bill.Days > 0
            ? bm * kwh + bs * 12
            : current?.YearlyKr(kwh);
        var saving = today is { } t && cheapest is not null ? t - cheapest.YearlyKr(kwh) : (double?)null;
        return new BillComparison(current, bill.MarkupKrPerKwh, portal, kwh, gap, cheapest, saving);
    }
}

// Reads the text of a Danish electricity bill (tested on NRGi's layout, written to be tolerant
// of others): the three parties, the product, period, kWh, total and every priced line.
// Pure text in, so it runs without any AI and costs nothing.
public static partial class ElBillParser
{
    private static readonly CultureInfo Da = new("da-DK");

    public static ElBill? Parse(string text)
    {
        var lines = text.Replace("\r", "").Split('\n').Select(l => Regex.Replace(l, @"\s+", " ").Trim()).Where(l => l.Length > 0).ToList();
        var all = string.Join("\n", lines);

        var supplier = First(all, @"Du har en elaftale med (.+)", @"Dit elselskab (.+)");
        var grid = First(all, @"Du har en elmåler fra (.+)", @"Dit netselskab (.+)");
        var product = First(all, @"Dit produkt er:\s*(.+)", @"Produkt:\s*(.+)");

        var period = PeriodRx().Match(all);
        DateOnly? from = period.Success ? D(period.Groups[1].Value) : null, to = period.Success ? D(period.Groups[2].Value) : null;
        var invoice = Regex.Match(all, @"Fakturadato\s+(\d{2}\.\d{2}\.\d{4})");

        var kwhMatch = Regex.Match(all, @"([\d.]+,\d+)\s*kWh");
        var totalMatch = Regex.Match(all, @"([\d.]+,\d{2})\s*kr\.?\s*Du skal i alt betale|Du skal i alt betale[^\d]*([\d.]+,\d{2})");

        // Priced lines, section by section.
        var bill = new List<ElBillLine>();
        var party = BillParty.Other;
        var inDetail = false;
        foreach (var l in lines)
        {
            if (l.Contains("Dit elselskab", StringComparison.OrdinalIgnoreCase)) { party = BillParty.Supplier; inDetail = true; continue; }
            if (l.StartsWith("Dit netselskab", StringComparison.OrdinalIgnoreCase)) { party = BillParty.Grid; continue; }
            if (l.Equals("Staten", StringComparison.OrdinalIgnoreCase) || l.StartsWith("Skatter og afgifter", StringComparison.OrdinalIgnoreCase)) { party = BillParty.State; continue; }
            if (!inDetail) continue;
            if (l.StartsWith("I alt", StringComparison.OrdinalIgnoreCase)) break;

            var m = RateLine().Match(l);
            if (m.Success)
            {
                bill.Add(new ElBillLine(party, m.Groups["name"].Value.Trim(), N(m.Groups["kwh"].Value), N(m.Groups["rate"].Value), N(m.Groups["kr"].Value)));
                continue;
            }
            m = AmountLine().Match(l);
            if (m.Success)
                bill.Add(new ElBillLine(party, m.Groups["name"].Value.Trim(), null, null, N(m.Groups["kr"].Value)));
        }

        var total = totalMatch.Success ? N(totalMatch.Groups[1].Success && totalMatch.Groups[1].Value.Length > 0 ? totalMatch.Groups[1].Value : totalMatch.Groups[2].Value) : bill.Sum(b => b.Kr);
        var kwh = kwhMatch.Success ? N(kwhMatch.Groups[1].Value) : bill.Select(b => b.Kwh ?? 0).DefaultIfEmpty(0).Max();

        if (supplier.Length == 0 && bill.Count == 0) return null; // not an electricity bill

        var meteringPoint = Regex.Match(all, @"\b(57\d{16})\b");
        var meter = Regex.Match(all, @"Målernr\.?:?\s*(\d{6,10})\b");
        if (!meter.Success && meteringPoint.Success)
        {
            // NRGi prints the labels first and the values after them: the meter number is the
            // number just before the 18-digit metering point id.
            var i = lines.FindIndex(x => x == meteringPoint.Groups[1].Value);
            if (i > 0 && Regex.IsMatch(lines[i - 1], @"^\d{6,10}$")) meter = Regex.Match(lines[i - 1], @"(\d+)");
        }

        return new ElBill(supplier, product, grid, from, to, invoice.Success ? D(invoice.Groups[1].Value) : null,
            kwh, total, bill,
            meter.Success ? meter.Groups[1].Value : "", meteringPoint.Success ? meteringPoint.Groups[1].Value : "");
    }

    // Plain-Danish explanation of a bill line, for "tryk for forklaring".
    public static string Explain(ElBillLine line)
    {
        var n = line.Name.ToLowerInvariant();
        if (n.StartsWith("rabat")) return "Et fradrag, som netselskabet giver. Det trækkes fra linjen nedenunder.";
        if (n.Contains("strømpris") || n.Contains("spotpris") || n.Contains("elpris")) return "Selve strømmen: prisen på elbørsen (Nord Pool) time for time, som elselskabet køber ind til. Den er den samme, uanset hvilket elselskab du har.";
        if (n.Contains("tillæg")) return "Elselskabets fortjeneste pr. kWh oven i børsprisen. Her er forskellen mellem elselskaberne – sammenlign under Elselskab.";
        if (n.Contains("abonnement") && line.Party == BillParty.Supplier) return "Elselskabets faste månedsbetaling. Varierer meget mellem selskaberne – nogle har 0 kr.";
        if (n.Contains("nettarif") && n.Contains("transmission")) return "Energinets betaling for de store højspændingsledninger, der fører strømmen tværs gennem landet. Ens for alle.";
        if (n.Contains("nettarif")) return "Netselskabets betaling for at føre strømmen gennem kablerne til dit hus. Dyrest kl. 17–21 – det er den, du sparer på ved at flytte forbrug.";
        if (n.Contains("net abo") || (n.Contains("abonnement") && line.Party == BillParty.Grid)) return "Netselskabets faste betaling for din måler og tilslutning. Den kan du ikke vælge fra.";
        if (n.Contains("elafgift")) return "Statens afgift på strøm. Den er sat ned til 1 øre pr. kWh fra 2026.";
        if (n.Contains("systemtarif")) return "Energinets betaling for at holde elsystemet stabilt og sikre forsyningen. Ens for alle.";
        if (n.Contains("tso") || n.Contains("system abonnement")) return "Energinets faste betaling som systemansvarlig (TSO). Ens for alle.";
        if (n.Contains("gebyr")) return "Et gebyr fra elselskabet – ofte for betalingsmåden. Kan tit undgås, fx med Betalingsservice.";
        return line.Party switch
        {
            BillParty.Supplier => "En del af elselskabets pris – den del du kan ændre ved at skifte elselskab.",
            BillParty.Grid => "En del af netselskabets pris – den samme, uanset elselskab.",
            BillParty.State => "Skat eller afgift til staten eller Energinet – ens for alle.",
            _ => "",
        };
    }

    public static string ExplainParty(BillParty p) => p switch
    {
        BillParty.Supplier => "Elselskabet sælger dig strømmen. Det kan du frit skifte – strømmen og leveringen er præcis den samme.",
        BillParty.Grid => "Netselskabet ejer kablerne og måleren og leverer strømmen til din dør. Det afhænger af din adresse og kan ikke skiftes.",
        BillParty.State => "Afgifter og tariffer til staten og Energinet. De er ens for alle i Danmark.",
        _ => "",
    };

    private static string First(string all, params string[] patterns)
    {
        foreach (var p in patterns)
        {
            var m = Regex.Match(all, p);
            if (m.Success) return m.Groups[1].Value.Trim().TrimEnd('.');
        }
        return "";
    }

    private static DateOnly? D(string s) =>
        DateOnly.TryParseExact(s, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static double N(string s) => double.Parse(s.Replace(".", ""), NumberStyles.Float, Da);

    [GeneratedRegex(@"PERIODEN:?\s*(\d{2}\.\d{2}\.\d{4})\s*-\s*(\d{2}\.\d{2}\.\d{4})", RegexOptions.IgnoreCase)]
    private static partial Regex PeriodRx();

    // "Strømpris 268,791 x 1,10717 297,60 kr."
    [GeneratedRegex(@"^(?<name>.+?)\s+(?<kwh>[\d.]+,\d+)\s*x\s*(?<rate>-?[\d.]+,\d+)\s+(?<kr>-?[\d.]+,\d{2})\s*kr\.?$")]
    private static partial Regex RateLine();

    // "Abonnement til NRGi Elhandel A/S 29,00 kr."
    [GeneratedRegex(@"^(?<name>[^\d-].*?)\s+(?<kr>-?[\d.]+,\d{2})\s*kr\.?$")]
    private static partial Regex AmountLine();
}
