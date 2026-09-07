using System.Globalization;
using System.Text.RegularExpressions;

namespace ITMartinBudget.Application.Helpers;

// "the category name should not be something like BS Skat. Remove BS and
// all that" (2026-09-07) - strips bank-channel noise (BS, Debetkort,
// MobilePay, DK-NOTAC/DK-NOTAZ reference prefixes, trailing order/reference
// numbers) from a category name so what's saved/shown reads like something a
// human would actually type, not a raw bank-export description. Distinct
// from CategoryDuplicateFinder.Normalize, which builds a lowercase MATCHING
// key for grouping - this produces the actual display/stored name, so
// casing and spacing are preserved (or improved), never lowercased wholesale.
public static class CategoryNameCleaner
{
    // Applied repeatedly from the start of the string until none match -
    // handles a doubled/stacked prefix like "MobilePay MobilePay Fie Tandru"
    // (both instances stripped, one per pass).
    private static readonly Regex[] LeadingNoisePatterns =
    [
        new(@"^debetkort\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^mobilepay\s*:?\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^mob\.?\s*pay\*?\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^forretning:\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // "fra " (Danish "from") on an incoming-transfer description like
        // "fra Nikolaj" - same leading-marker-only caveat as every other
        // pattern here (see CategoryDuplicateFinder.Normalize's matching
        // twin of this rule).
        new(@"^fra\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // "BS " (Betalingsservice, a direct-debit marker) only means
        // anything as a leading token - stripping it anywhere else would eat
        // the "bs" inside an unrelated word (same caveat as
        // CategoryDuplicateFinder.Normalize).
        new(@"^bs\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Reference-number prefixes on card/BS transactions -
        // "DK-NOTACDKIG TELENOR.D", "DK-NOTAZ5486 INGO ÅRHUS" - the letters
        // right after DK-NOTA vary, always followed by more reference
        // characters then a space before the real merchant name.
        new(@"^DK-NOTA[A-Z0-9]+\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    // Trailing order/reference numbers - "Shell Service - 92",
    // "Q8 Service - 8015".
    private static readonly Regex TrailingReferenceNumber =
        new(@"\s*-\s*\d+$", RegexOptions.Compiled);

    private static readonly CultureInfo DanishCulture = CultureInfo.GetCultureInfo("da-DK");

    public static string Clean(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return rawName;

        var s = rawName.Trim();

        bool changed;
        do
        {
            changed = false;
            foreach (var pattern in LeadingNoisePatterns)
            {
                var next = pattern.Replace(s, "").TrimStart();
                if (next != s)
                {
                    s = next;
                    changed = true;
                }
            }
        } while (changed && s.Length > 0);

        s = TrailingReferenceNumber.Replace(s, "");
        s = Regex.Replace(s, @"\s+", " ").Trim();

        // Stripping every prefix left nothing real behind (the whole string
        // was noise) - fall back to the original rather than saving an
        // empty category name.
        if (s.Length == 0) return rawName.Trim();

        // A whole-caps bank-export artifact ("MOBILEPAY FIE TANDRU" once its
        // own prefix is stripped becomes "FIE TANDRU") reads as shouting -
        // title-case it. Mixed-case input (already reasonable) is left
        // exactly as it came in.
        if (s.Any(char.IsLower) is false && s.Any(char.IsUpper))
        {
            s = DanishCulture.TextInfo.ToTitleCase(s.ToLower(DanishCulture));
        }

        return s;
    }
}
