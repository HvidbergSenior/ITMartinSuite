using System.Globalization;
using System.Text.RegularExpressions;

namespace ITMartin.Receipt.Application.Services;

// "Did I get the correct rabat?" (user 2026-11-19): every bought line is compared with that shop's offers in Tilbud
// for the purchase date. Paid more than the offer price = a "Tjek" note - never "fejl", because a receipt line and an
// offer can be two sizes of the same thing. Free: Tilbud's own API, at most MaxLookups searches per receipt.

/// <summary>An offer as Tilbud gives it.</summary>
public sealed record OfferInfo(string Chain, string Title, decimal Price, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo,
    string? NeedsApp, string? Condition);

/// <summary>Tilbud's GET /api/tilbud (Receipt__TilbudUrl).</summary>
public interface IOfferLookup
{
    Task<IReadOnlyList<OfferInfo>> SearchAsync(string query, double lat, double lng, CancellationToken ct = default);
}

public sealed record DiscountNote(string Line, decimal Paid, decimal OfferPrice, string OfferTitle, string Text);

public sealed class DiscountCheck(IOfferLookup offers)
{
    public const int MaxLookups = 12;
    private static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
        { "stk", "pk", "pakke", "kg", "gr", "ltr", "dansk", "danske", "øko", "økologisk", "frisk", "stor", "lille", "pant", "pose", "bærepose" };

    public async Task<IReadOnlyList<DiscountNote>> CheckAsync(string? merchant, DateTime? purchaseDate, IReadOnlyList<ReceiptLine> lines,
        double lat, double lng, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(merchant) || lines.Count == 0) return [];
        var day = purchaseDate ?? DateTime.Today;
        var notes = new List<DiscountNote>();
        foreach (var line in lines.Where(l => l.Paid >= 5).Take(MaxLookups))
        {
            var words = Words(line.Description);
            if (words.Count == 0) continue;
            IReadOnlyList<OfferInfo> found;
            try { found = await offers.SearchAsync(string.Join(' ', words.Take(2)), lat, lng, ct); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { break; }   // Tilbud down: no notes rather than wrong ones

            var best = found
                .Where(o => SameShop(o.Chain, merchant) && ValidOn(o, day))
                .Select(o => (o, score: Overlap(words, Words(o.Title))))
                .Where(x => x.score >= Math.Min(2, words.Count))
                .OrderByDescending(x => x.score).ThenBy(x => x.o.Price)
                .Select(x => x.o).FirstOrDefault();
            if (best is null || best.Price + 0.5m >= line.Paid) continue;

            var extra = best.NeedsApp is { } app ? $" (kun med {app})" : best.Condition is { } c ? $" ({c})" : "";
            notes.Add(new DiscountNote(line.Description, line.Paid, best.Price, best.Title,
                $"{line.Description}: tilbud {Kr(best.Price)} kr hos {best.Chain}{extra} – du betalte {Kr(line.Paid)} kr. Tjek kvitteringen."));
        }
        return notes;
    }

    public static List<string> Words(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"[a-zæøåé]{3,}").Select(m => m.Value).Where(w => !Skip.Contains(w)).Distinct().ToList();

    /// <summary>Words that start alike count ("kaff" in KAFFE / kaffe, "form" in FORM. / formalet) - receipts shorten words.</summary>
    public static int Overlap(IReadOnlyList<string> a, IReadOnlyList<string> b) =>
        a.Count(x => b.Any(y => y.StartsWith(x[..Math.Min(4, x.Length)]) || x.StartsWith(y[..Math.Min(4, y.Length)])));

    public static bool SameShop(string chain, string merchant)
    {
        var c = chain.ToLowerInvariant().Replace("føtex", "fotex"); var m = merchant.ToLowerInvariant().Replace("føtex", "fotex");
        var key = c.Split(' ')[0];
        return m.Contains(key) || c.Contains(m.Split(' ')[0]);
    }

    private static bool ValidOn(OfferInfo o, DateTime day) =>
        (o.ValidFrom is null || o.ValidFrom.Value.Date <= day.Date) && (o.ValidTo is null || o.ValidTo.Value.Date >= day.Date);

    private static string Kr(decimal v) => v.ToString(v % 1 == 0 ? "0" : "0.00", CultureInfo.GetCultureInfo("da-DK"));
}
