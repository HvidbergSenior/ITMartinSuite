using System.Globalization;
using System.Text;

namespace ITMartinHjem.Domain;

// "Mine ønsker" on bogshoppen.dk (user 2026-11-19): a customer with a kundenummer writes what they look for ("Anders And",
// "Bones DVD"). When a product that fits comes into the shop, a notice pops up on the site with their number.
public sealed class CustomerWish
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";   // the kundenummer
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>A found product the customer has already been told about - the notice does not pop up for it again.</summary>
public sealed class WishSeen
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public int ItemId { get; set; }
    public DateTime At { get; set; }
}

public static class WishRules
{
    public const int MaxText = 80, MaxPerCustomer = 20, MaxFoundPerWish = 6;

    // A word for the kind of thing narrows the search instead of being looked for in the title ("Bones DVD").
    private static readonly Dictionary<string, string> KindWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dvd"] = "DVD", ["dvd'er"] = "DVD", ["blu-ray"] = "Blu-ray", ["bluray"] = "Blu-ray", ["cd"] = "CD", ["cd'er"] = "CD",
        ["vinyl"] = "Vinyl", ["lp"] = "Vinyl", ["vhs"] = "VHS",
    };

    // Small words that say nothing about the product.
    private static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase)
        { "og", "eller", "en", "et", "the", "på", "af", "med", "til", "jeg", "leder", "efter", "gerne", "noget" };

    /// <returns>The error for the person, or null when the wish can be saved.</returns>
    public static string? Check(string? text, int alreadyHas)
    {
        var t = Clean(text);
        if (t.Length < 2) return "Skriv hvad du leder efter, fx Anders And eller Bones DVD.";
        if (t.Length > MaxText) return $"Hold ønsket under {MaxText} tegn.";
        if (alreadyHas >= MaxPerCustomer) return $"Du kan have {MaxPerCustomer} ønsker. Fjern et, før du skriver et nyt.";
        return null;
    }

    public static string Clean(string? text) => BogshoppenWishes.Clean(text);

    /// <summary>"Bones DVD" -> search "Bones", kind DVD. Kind is null when the wish names none.</summary>
    public static (string Search, string? Kind) Parse(string wish)
    {
        string? kind = null;
        var words = new List<string>();
        foreach (var w in Words(wish))
            if (KindWords.TryGetValue(w, out var k)) kind ??= k;
            else if (!Filler.Contains(w)) words.Add(w);
        return (string.Join(' ', words), kind);
    }

    /// <summary>Every word of the wish (kind words and small words aside) is in the product's title, line or kind.</summary>
    public static bool Matches(string wish, string title, string line, string kind)
    {
        var (search, wantKind) = Parse(wish);
        if (wantKind is not null && !string.Equals(kind, wantKind, StringComparison.OrdinalIgnoreCase)) return false;
        var hay = Words(Fold($"{title} {line} {kind}").Replace('&', ' ')).ToList();
        var words = search.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Fold).ToList();
        // A whole word, or the start of one for longer words ("sæson" fits "sæsonen") - "and" never fits "Andersen".
        return words.Count > 0 && words.All(w => hay.Any(h => h == w || (w.Length >= 4 && h.StartsWith(w, StringComparison.Ordinal))));
    }

    private static IEnumerable<string> Words(string s) =>
        s.Split([' ', ',', '.', ':', ';', '!', '?', '(', ')', '"'], StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 0);

    // Lower case, accents off (é -> e) but æ ø å kept, "-" as a space.
    private static string Fold(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.ToLowerInvariant().Replace('-', ' ').Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark || c == '̊') sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
