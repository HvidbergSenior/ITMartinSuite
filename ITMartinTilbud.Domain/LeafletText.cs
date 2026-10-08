using System.Globalization;
using System.Text.RegularExpressions;

namespace ITMartinTilbud.Domain;

/// <summary>What the leaflet's own words say about an offer: does it need the shop's app, what does the app give,
/// is there a condition, is it a one-day deal. Read from how the leaflets print it (checked on all week-41 leaflets).</summary>
public static class LeafletText
{
    // What the app does to this offer:
    //   Coop  "Pris ikke-medlem 79,95. Medlemspris 55,97."      Netto "+ PRIS 20:"     Bilka "PLUS PRIS FRIT VALG 89.-"
    //   two unit prices "Pr. kg max. 57,69 plus pris Pr. kg max 38,46" - the normal one first, then the app one.
    // Lidl's "Lidl Plus"/"Kuponpris": the price shown IS the app price, the normal one is usually not printed.
    public static (string? App, bool Only, decimal? AppPrice, decimal? NoAppPrice) AppInfo(string text, string chain, decimal price, decimal? unitPrice)
    {
        var app = AppNeeded(text, chain);
        if (app is null) return (null, false, null, null);
        static decimal? Num(string s) => decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : null;
        static bool Same(decimal a, decimal b) => Math.Abs(a - b) <= Math.Max(0.5m, b * 0.02m);

        var notMember = Regex.Match(text, @"(?i)pris ikke-medlem\.?\s*(\d+(?:[.,]\d+)?)");
        var member = Regex.Match(text, @"(?i)medlemspris\.?\s*(\d+(?:[.,]\d+)?)");
        var nm = notMember.Success ? Num(notMember.Groups[1].Value) : null;
        var mp = member.Success ? Num(member.Groups[1].Value) : null;
        if (nm is { } n && mp is { } m && m < n)
            return Same(price, m) ? (app, true, null, n) : (app, false, m, null);

        var explicitApp = Regex.Match(text, @"(?i)(?:\+ ?pris|plus ?pris)(?: frit valg)?\s*(\d+(?:,\d+)?)\s*(?:\.-|,-|[:\-]|kr)");
        if (explicitApp.Success && Num(explicitApp.Groups[1].Value) is { } ap && ap < price) return (app, false, ap, null);

        var units = Regex.Matches(text, @"(?i)(?:pr\.?\s*(?:kg|l|liter|ltr)\.?|literpris|kg ?pris)\s*(?:max\.?|maks\.?)?\s*(\d+[.,]\d{2})")
            .Select(x => Num(x.Groups[1].Value)).Where(v => v is not null).Select(v => v!.Value).Distinct().Take(2).ToList();
        if (units.Count == 2)
        {
            var (hi, lo) = (Math.Max(units[0], units[1]), Math.Min(units[0], units[1]));
            // Which of the two is the price shown? The leaflets print the normal one first. Our own per-kg price only decides
            // when it matches one of them closely - for "900-2500 g" packs it is computed on another size and misleads.
            static bool Near(decimal a, decimal b) => Math.Abs(a - b) <= b * 0.03m;
            var shownIsNormal = unitPrice is { } u && Near(u, hi) ? true : unitPrice is { } u2 && Near(u2, lo) ? false : units[0] == hi;
            return shownIsNormal ? (app, false, Math.Round(price * lo / hi, 2), null) : (app, true, null, Math.Round(price * hi / lo, 2));
        }
        // Nothing to read the difference from: Lidl's coupon prices are app prices; the others' plus prices come on top.
        var lidlish = chain.Equals("Lidl", StringComparison.OrdinalIgnoreCase) || text.Contains("Kuponpris", StringComparison.OrdinalIgnoreCase);
        return (app, lidlish, null, null);
    }

    // Offers that need the shop's own app (user 2026-10-06: "too complicated for a lot of people") - as the leaflet words it:
    // "Gælder kun med Netto+ appen", "plus pris … føtex plus appen", "Kuponpris … Lidl Plus", "medlemspris" (Coop).
    public static string? AppNeeded(string text, string chain)
    {
        var t = text.ToLowerInvariant();
        var m = Regex.Match(t, @"kun med ([a-zæøå0-9+ ]{2,25}?app(?:en)?)\b");
        if (m.Success) return Cap(m.Groups[1].Value.Trim());
        if (t.Contains("lidl plus") || t.Contains("kuponpris")) return "Lidl Plus-appen";
        if (t.Contains("medlemspris") || t.Contains("coop app") || t.Contains("coop-app") || t.Contains("kun for medlemmer")) return "Coop-appen (medlem)";
        if (t.Contains("plus pris") || t.Contains("+ pris") || t.Contains("+pris")) return chain + "s app";
        return null;
    }

    // "Kuponpris ved køb på min. 200 kr." -> "kræver køb for min. 200 kr"
    public static string? ConditionOf(string text)
    {
        var m = Regex.Match(text, @"(?i)ved køb (?:på|for|af) (?:min\.?|minimum) ?(\d+) ?kr");
        return m.Success ? $"kræver køb for min. {m.Groups[1].Value} kr" : null;
    }

    private static readonly string[] Months = ["jan", "feb", "mar", "apr", "maj", "jun", "jul", "aug", "sep", "okt", "nov", "dec"];

    // "Fredagsdeal – Gælder kun 9. okt." -> only that day (Danish time), though the leaflet runs all week.
    public static DateTimeOffset? OnlyDay(string text, DateTimeOffset reference)
    {
        var m = Regex.Match(text, @"(?i)gælder kun (\d{1,2})\. ?(jan|feb|mar|apr|maj|jun|jul|aug|sep|okt|nov|dec)");
        if (!m.Success) return null;
        var month = Array.IndexOf(Months, m.Groups[2].Value.ToLowerInvariant()) + 1;
        var dk = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Copenhagen");
        var year = reference.Year + (month < reference.Month - 6 ? 1 : 0);
        if (!DateTime.TryParse($"{year}-{month:00}-{int.Parse(m.Groups[1].Value):00}", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return null;
        return new DateTimeOffset(d, dk.GetUtcOffset(d));
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];
}
