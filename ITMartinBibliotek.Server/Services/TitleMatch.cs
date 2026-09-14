using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ITMartinBibliotek.Server.Services;

// Title normalisation used both when matching Jellyfin items to the catalog
// and when turning a barcode-database product name into a searchable title.
public static partial class TitleMatch
{
    [GeneratedRegex(@"\((dvd|blu-?ray|bd|cd|\d+ disc|\d+-disc|region \d|widescreen|special edition|collector'?s edition)\)", RegexOptions.IgnoreCase)]
    private static partial Regex ParenNoise();

    [GeneratedRegex(@"\b(dvd|blu-?ray|bluray|bd|4k uhd|4k|uhd|widescreen|special edition|steelbook|collector'?s edition|\d+-disc|disc \d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WordNoise();

    [GeneratedRegex(@"[^a-z0-9 ]")]
    private static partial Regex NonAlnum();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex YearRx();

    // "The Matrix (DVD) [1999]" -> "matrix 1999"; "Blade Runner" == "Blade Runner: The Final Cut"? No - kept strict.
    public static string Normalize(string title)
    {
        var s = title.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        s = sb.ToString().ToLowerInvariant();
        s = ParenNoise().Replace(s, " ");
        s = WordNoise().Replace(s, " ");
        s = NonAlnum().Replace(s, " ");
        s = Spaces().Replace(s, " ").Trim();
        if (s.StartsWith("the ")) s = s[4..];
        return s;
    }

    // Strip the format noise from a product name so it makes a decent search query.
    public static string CleanProductName(string name)
    {
        var s = ParenNoise().Replace(name, " ");
        s = WordNoise().Replace(s, " ");
        s = s.Replace("[", " ").Replace("]", " ");
        return Spaces().Replace(s, " ").Trim(' ', '-', ':');
    }

    public static int? YearIn(string text)
    {
        var m = YearRx().Match(text);
        return m.Success ? int.Parse(m.Value) : null;
    }

    public static bool Same(string a, int? yearA, string b, int? yearB)
    {
        if (Normalize(a) != Normalize(b)) return false;
        if (yearA is null || yearB is null) return true;
        return Math.Abs(yearA.Value - yearB.Value) <= 1;
    }
}
