using System.Globalization;
using System.Text.RegularExpressions;

namespace ITMartin.Receipt.Application.Services;

/// <summary>What the free OCR text alone tells: the shop, the total and the date. Null fields = not found.</summary>
public sealed record OcrReceiptFacts(string? Merchant, decimal? Total, DateTime? Date);

/// <summary>
/// 2026-11-19 (Ugens indkøb, "no AI cost"): read a Danish grocery receipt's total from the OCR text with plain rules,
/// so most receipts never need an AI call. The total is the amount on the LAST line that says TOTAL / I ALT / AT BETALE /
/// BETALT / SUM (not "subtotal", not "moms", not "rabat"); if none, the largest amount near the bottom is NOT guessed - null.
/// </summary>
public static partial class ReceiptTotalReader
{
    private static readonly string[] Shops = ["lidl", "føtex", "fotex", "netto", "rema", "løvbjerg", "lovbjerg", "bilka", "kvickly", "superbrugsen", "coop", "meny", "spar", "aldi"];

    [GeneratedRegex(@"(-?\d{1,3}(?:[.\s]\d{3})*[,.]\d{2})(?!\d)")] private static partial Regex Amount();
    [GeneratedRegex(@"\b(total|i alt|ialt|at betale|betalt|beløb|sum|dankort|visa|mastercard|mobilepay)\b", RegexOptions.IgnoreCase)] private static partial Regex TotalWord();
    [GeneratedRegex(@"\b(sub ?total|moms|rabat|spar|pant|byttepenge|retur)\b", RegexOptions.IgnoreCase)] private static partial Regex NotTotal();
    [GeneratedRegex(@"\b(\d{1,2})[.\-/](\d{1,2})[.\-/](\d{2,4})\b")] private static partial Regex DateRx();

    public static OcrReceiptFacts Read(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText)) return new(null, null, null);
        var lines = ocrText.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        var head = string.Join(" ", lines.Take(8)).ToLowerInvariant();
        var merchant = Shops.FirstOrDefault(s => head.Contains(s)) ?? Shops.FirstOrDefault(s => ocrText.ToLowerInvariant().Contains(s));

        decimal? total = null;
        // Strong words first (TOTAL / I ALT / AT BETALE), then the card line - the last match wins (totals sit near the bottom).
        foreach (var strong in new[] { true, false })
        {
            foreach (var l in lines)
            {
                if (!TotalWord().IsMatch(l) || NotTotal().IsMatch(l)) continue;
                var isStrong = Regex.IsMatch(l, @"\b(total|i alt|ialt|at betale)\b", RegexOptions.IgnoreCase);
                if (strong != isStrong) continue;
                var m = Amount().Matches(l).LastOrDefault();
                if (m is not null && Parse(m.Groups[1].Value) is { } v && v > 0 && v < 20000) total = v;
            }
            if (total is not null) break;
        }

        DateTime? date = null;
        foreach (var l in lines)
        {
            var dm = DateRx().Match(l);
            if (!dm.Success) continue;
            var y = int.Parse(dm.Groups[3].Value); if (y < 100) y += 2000;
            if (int.TryParse(dm.Groups[1].Value, out var d) && int.TryParse(dm.Groups[2].Value, out var mo) && mo is >= 1 and <= 12 && d is >= 1 and <= 31 && y is >= 2020 and <= 2100
                && DateTime.TryParseExact($"{y:0000}-{mo:00}-{d:00}", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            { date = dt; break; }
        }
        return new(merchant, total, date);
    }

    // "1.234,50" / "1 234,50" / "234,50" / "234.50" -> decimal
    private static decimal? Parse(string s)
    {
        s = s.Replace(" ", "");
        var lastSep = Math.Max(s.LastIndexOf(','), s.LastIndexOf('.'));
        if (lastSep < 0) return null;
        var whole = s[..lastSep].Replace(".", "").Replace(",", "");
        var frac = s[(lastSep + 1)..];
        return decimal.TryParse(whole + "." + frac, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
