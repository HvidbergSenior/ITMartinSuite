using System.Globalization;
using System.Text.RegularExpressions;

namespace ITMartin.Receipt.Application.Services;

// 2026-11-19 (user: "upload pictures will be the main data input", "screenshots of an offer in the Lidl app should be
// uploaded and added to indkøb", "kvittering ... did I get the correct rabat"). One free reader for any uploaded picture:
// OCR text + plain rules, no AI. It says WHAT the picture is and reads what matters for that kind.

public enum PictureKind { Ukendt, Kvittering, Tilbud }

/// <summary>One bought line. Discount = what the receipt took off this line (positive kr), with the receipt's own words.</summary>
public sealed record ReceiptLine(string Description, decimal Price, decimal Discount = 0, string? DiscountLabel = null)
{
    public decimal Paid => Price - Discount;
}

/// <summary>An offer read from a screenshot (shop app, leaflet). AppOnly = only with the shop's app/card ("Lidl Plus").</summary>
public sealed record OfferShot(string? Shop, string Product, decimal Price, bool AppOnly, string? App);

public sealed record PictureReading(PictureKind Kind, OcrReceiptFacts Facts, IReadOnlyList<ReceiptLine> Lines, OfferShot? Offer);

public static partial class PictureReader
{
    [GeneratedRegex(@"(-?\d{1,4}(?:\.\d{3})?[,.]\d{2})(?:\s*-)?\s*[A-Z]?\s*$")] private static partial Regex EndAmount();
    [GeneratedRegex(@"(\d{1,4})[,.](\d{2})|(\d{1,4}),-|(\d{1,4}):-")] private static partial Regex AnyPrice();
    [GeneratedRegex(@"\b(total|i alt|ialt|subtotal|sub total|moms|dankort|visa|mastercard|mobilepay|byttepenge|kontant|retur|betalt|at betale|beløb|kortbetaling|antal varer|cvr|tlf|telefon|kvittering|bon nr|kasse|ekspedient)\b", RegexOptions.IgnoreCase)] private static partial Regex NotItem();
    [GeneratedRegex(@"\b(rabat|spar|besparelse|tilbud|plus|kupon|coupon|bonus|medlemspris|pris ned|nedsat|mængderabat)\b", RegexOptions.IgnoreCase)] private static partial Regex DiscountWord();
    [GeneratedRegex(@"^\s*\d+\s*(x|stk\.?|á|a)\s", RegexOptions.IgnoreCase)] private static partial Regex QuantityLine();
    [GeneratedRegex(@"\b(pr\.?\s*(kg|l|ltr|stk|100\s*g)|kg-pris|literpris|før|normalpris|spar)\b", RegexOptions.IgnoreCase)] private static partial Regex NotThePrice();
    [GeneratedRegex(@"\b(tilbud|lidl plus|netto\+|plus pris|medlemspris|kun med|gælder|gyldig|spar|kupon|coupon|-\d{1,2}\s?%|aktivér|aktiver)\b", RegexOptions.IgnoreCase)] private static partial Regex OfferWord();

    private static readonly (string Word, string Shop)[] Shops =
    [
        ("lidl", "Lidl"), ("føtex", "Føtex"), ("fotex", "Føtex"), ("netto", "Netto"), ("rema", "Rema 1000"), ("løvbjerg", "Løvbjerg"),
        ("bilka", "Bilka"), ("kvickly", "Kvickly"), ("superbrugsen", "SuperBrugsen"), ("coop", "Coop"), ("meny", "Meny"), ("spar", "Spar"),
    ];

    public static PictureReading Read(string? ocrText)
    {
        var facts = ReceiptTotalReader.Read(ocrText);
        if (string.IsNullOrWhiteSpace(ocrText)) return new(PictureKind.Ukendt, facts, [], null);
        var lines = ocrText.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        var prices = AnyPrice().Matches(ocrText).Count;

        if (facts.Total is > 0 && prices >= 3)
            return new(PictureKind.Kvittering, facts, ReceiptLines(lines), null);
        if (prices is >= 1 and <= 8 && Offer(lines) is { } offer)
            return new(PictureKind.Tilbud, facts, [], offer);
        return new(PictureKind.Ukendt, facts, [], null);
    }

    /// <summary>The bought lines: text + amount at the end; a discount line ("RABAT -5,00", "Lidl Plus -2,00") belongs to the line above.</summary>
    public static List<ReceiptLine> ReceiptLines(IReadOnlyList<string> lines)
    {
        var items = new List<ReceiptLine>();
        foreach (var raw in lines)
        {
            var m = EndAmount().Match(raw);
            if (!m.Success) continue;
            var desc = raw[..m.Index].Trim().TrimEnd('-', ':', '.').Trim();
            var negative = m.Groups[1].Value.StartsWith('-') || raw.TrimEnd().EndsWith('-');
            if (Parse(m.Groups[1].Value.TrimStart('-')) is not { } amount || amount == 0) continue;
            if (Regex.IsMatch(desc, @"\b(total|i alt|ialt|at betale)\b", RegexOptions.IgnoreCase)) break;   // the end of the lines
            if (NotItem().IsMatch(desc) || QuantityLine().IsMatch(raw)) continue;
            if (desc.Count(char.IsLetter) < 2) continue;

            if ((negative || DiscountWord().IsMatch(desc)) && items.Count > 0)
            {
                var last = items[^1];
                items[^1] = last with { Discount = last.Discount + amount, DiscountLabel = last.DiscountLabel is null ? desc : last.DiscountLabel + ", " + desc };
                continue;
            }
            if (negative) continue;
            if (amount > 5000) continue;
            items.Add(new ReceiptLine(Clean(desc), amount));
        }
        return items;
    }

    /// <summary>A screenshot of one offer: the price is the first price that is not a unit price or "før"; the product is the
    /// text line just above it that is not a shop, app or offer word. Null = this does not look like one offer.</summary>
    public static OfferShot? Offer(IReadOnlyList<string> lines)
    {
        var all = string.Join(' ', lines).ToLowerInvariant();
        var shop = Shops.FirstOrDefault(s => all.Contains(s.Word)).Shop;
        var looksLikeOffer = OfferWord().IsMatch(all) || shop is not null;
        if (!looksLikeOffer) return null;

        for (var i = 0; i < lines.Count; i++)
        {
            if (NotThePrice().IsMatch(lines[i])) continue;
            var pm = AnyPrice().Match(lines[i]);
            if (!pm.Success) continue;
            var price = pm.Groups[1].Success ? Parse(pm.Groups[1].Value + "," + pm.Groups[2].Value)
                : decimal.TryParse(pm.Groups[3].Success ? pm.Groups[3].Value : pm.Groups[4].Value, out var whole) ? whole : null;
            if (price is not > 0 || price > 2000) continue;

            // The product: same line before the price, else the nearest line above with real words.
            var before = Clean(lines[i][..pm.Index]);
            var product = IsProductText(before) ? before : null;
            for (var j = i - 1; product is null && j >= 0 && j >= i - 4; j--)
                if (IsProductText(lines[j])) product = Clean(lines[j]);
            for (var j = i + 1; product is null && j < lines.Count && j <= i + 2; j++)
                if (IsProductText(lines[j])) product = Clean(lines[j]);
            if (product is null) return null;

            var app = all.Contains("lidl plus") ? "Lidl Plus" : all.Contains("netto+") || all.Contains("netto plus") ? "Netto+"
                : all.Contains("plus pris") || all.Contains("pluspris") ? (shop is null ? "Plus" : shop + " Plus")
                : all.Contains("medlemspris") || all.Contains("coop app") ? "Coop-appen" : null;
            var appOnly = app is not null && (all.Contains("kun med") || all.Contains("aktivér") || all.Contains("aktiver") || all.Contains("lidl plus") || all.Contains("kupon"));
            return new OfferShot(shop, product, price.Value, appOnly, app);
        }
        return null;
    }

    private static bool IsProductText(string s)
    {
        var t = s.Trim();
        if (t.Count(char.IsLetter) < 3 || t.Length > 70) return false;
        var low = t.ToLowerInvariant();
        if (Shops.Any(x => low == x.Word || low == x.Word + " plus")) return false;
        if (NotThePrice().IsMatch(t) || NotItem().IsMatch(t)) return false;
        if (Regex.IsMatch(low, @"^(tilbud|spar|kun med|gælder|gyldig|aktivér|aktiver|tilføj|se mere|kupon|coupon|lidl plus|netto\+|ugens|pr\.?\s)")) return false;
        return true;
    }

    private static string Clean(string s) => Regex.Replace(s, @"\s{2,}", " ").Trim(' ', '-', ':', '*', '.', ',');

    private static decimal? Parse(string s)
    {
        s = s.Replace(" ", "");
        var lastSep = Math.Max(s.LastIndexOf(','), s.LastIndexOf('.'));
        if (lastSep < 0) return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var w) ? w : null;
        var whole = s[..lastSep].Replace(".", "").Replace(",", "");
        var frac = s[(lastSep + 1)..];
        return decimal.TryParse(whole + "." + frac, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
