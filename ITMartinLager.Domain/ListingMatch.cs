using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ITMartinLager.Domain;

// Rules from the first Stikprøve (user 2026-11-24: 59 listings marked, 30 right / 29 wrong). A listing that breaks one of them never
// counts for the price:
// 1. a TV season must be the SAME season, and only that one ("Second Season" is not "sæson 1-7" or "sæson 5");
// 2. a CD whose title is only the artist (no album name) gets no price from listings at all ("Rufus Wainwright" matched every album);
// 3. several things at once ("2 cd'er 25 kr i alt", "CD albums", "samling") is not one thing;
// 4. every word of our own title must be in the listing ("Desserter – Sund & slank" is not "Frokoster – Sund & slank").
public static partial class ListingMatch
{
    /// <summary>True when the listing is NOT our thing by these rules.</summary>
    public static bool Wrong(Item i, string listing)
    {
        listing ??= "";
        if (Films.IsSeason(i.Kind, i.Title) && !SameSeason(i.Title, listing)) return true;
        if (Bundle(listing, i)) return true;
        if (NoIssueNumber(i, listing)) return true;
        if (Sequel(i, listing)) return true;
        return !TitleWordsIn(i, listing);
    }

    /// <summary>Rule 2: a CD/LP whose title is just the artist - no album to look for.</summary>
    // 2026-11-24 (user, Stikprøve: Sanne Salomonsen's album "Sanne"): also when every word of the title is in the artist's name -
    // then every listing of that artist matches (Evita, Freedom, 1996 ...).
    public static bool NoAlbum(Item i)
    {
        // DVD/Blu-ray too (round 2: stand-up DVD "Mick Øgendahl" matched Spas, Micktrix, Julefrokosten ...).
        if (i.Kind is not (Kinds.Cd or Kinds.Vinyl or Kinds.Dvd or Kinds.BluRay) || i.Artist.Trim() == "") return false;
        var title = Words(i.Title).ToList();
        if (title.Count == 0) return false;
        var artist = Words(i.Artist).ToHashSet();
        return title.All(artist.Contains);
    }

    // ---------- 1. seasons ----------
    private static readonly Dictionary<string, int> Ordinals = new(StringComparer.OrdinalIgnoreCase)
    {
        ["first"] = 1, ["second"] = 2, ["third"] = 3, ["fourth"] = 4, ["fifth"] = 5, ["sixth"] = 6, ["seventh"] = 7, ["eighth"] = 8,
        ["ninth"] = 9, ["tenth"] = 10, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7,
        ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["første"] = 1, ["anden"] = 2, ["tredje"] = 3, ["fjerde"] = 4, ["femte"] = 5,
    };

    /// <summary>Every season number a text names ("sæson 1-7" = 1..7, "sæson 3, 4 og 8" = 3, 4, 8, "The Second Season" = 2).</summary>
    public static HashSet<int> Seasons(string text)
    {
        var found = new HashSet<int>();
        foreach (Match m in SeasonNumbersRx().Matches(text))
        {
            var list = m.Groups["list"].Value;
            foreach (Match r in RangeRx().Matches(list))
            {
                var a = int.Parse(r.Groups[1].Value);
                var b = r.Groups[2].Success ? int.Parse(r.Groups[2].Value) : a;
                for (var n = Math.Min(a, b); n <= Math.Max(a, b) && n - Math.Min(a, b) < 30; n++) found.Add(n);
            }
        }
        foreach (Match m in OrdinalSeasonRx().Matches(text))
            if (Ordinals.TryGetValue(m.Groups["w"].Value, out var n)) found.Add(n);
        return found;
    }

    /// <summary>Rule 1: the listing names exactly our season. A listing that names no season at all is not proof either.</summary>
    public static bool SameSeason(string ourTitle, string listing)
    {
        var ours = Seasons(ourTitle);
        if (ours.Count != 1) return true;   // we do not know our own season - leave it to the other rules
        var theirs = Seasons(listing);
        return theirs.Count == 1 && theirs.SetEquals(ours);
    }

    // ---------- 2b. a numbered blad: the listing must name OUR number ----------
    // Round 2 (2026-11-24): "Sølvpil 1978" / "SØLVPIL 1978, Tegneserie" for our Sølvpil nr. 48 from 1978 - could be any of that year's issues.
    public static bool NoIssueNumber(Item i, string listing)
    {
        var n = i.Number.Trim().TrimStart('0');
        if (!FixedPrice.Magazines.Contains(i.Kind) || n == "" || !n.All(char.IsDigit)) return false;
        return !Regex.IsMatch(listing, $@"(?<!\d)0*{n}(?!\d)");
    }

    // ---------- 2c. a sequel ----------
    // Round 3 (2026-11-24): "Anugama - Shamanic Dream II" is not our "Shamanic Dream". Right after our title the listing has a
    // part marker (II, 2, part 2, vol 2, del 2) that our own title does not have.
    private static readonly HashSet<string> PartMarks = ["ii", "iii", "iv", "vi", "vii", "viii", "ix", "2", "3", "4", "5", "6", "7", "8", "9", "part", "vol", "del", "volume"];

    public static bool Sequel(Item i, string listing)
    {
        var ours = Words(i.Title).ToList();
        var key = ours.Where(w => w.Length >= 3 && !Stop.Contains(w) && !w.All(char.IsDigit)).ToHashSet();
        if (key.Count == 0) return false;
        var theirs = Words(listing).ToList();
        for (var k = 0; k < theirs.Count - 1; k++)
        {
            var mark = theirs[k + 1];
            if (!key.Contains(theirs[k]) || !PartMarks.Contains(mark) || ours.Contains(mark)) continue;
            // "Emancipation (3 x CD Album)" / "2 cd" is a count of discs, not part 3.
            if (mark.All(char.IsDigit) && k + 2 < theirs.Count && theirs[k + 2] is "x" or "cd" or "cds" or "disc" or "discs" or "stk" or "dvd") continue;
            return true;
        }
        return false;
    }

    // ---------- 3. several things at once ----------
    private static readonly string[] BundleWords =
        [" i alt", "albums", "samling", "pakke", "bundle", "cd'er", "cd’er", "dvd'er", "dvd’er", "bøger", "blade sælges", " lot ", "stk."];

    public static bool Bundle(string listing, Item i)
    {
        var l = " " + listing.ToLowerInvariant() + " ";
        var own = " " + FixedPrice.OwnText(i).ToLowerInvariant() + " ";
        return BundleWords.Any(w => l.Contains(w) && !own.Contains(w))
            // "3 x Cd", "10 stk", "4 titler" (2026-11-24, Sanne: "3 x CD" with our album bottom left). Two is left out: "2 x CD" is a double album.
            || (CountRx().IsMatch(l) && !CountRx().IsMatch(own));
    }

    [GeneratedRegex(@"\b([3-9]|\d{2,})\s*(x|stk|styk|stykker|titler)\b(?!\s*(cd|lp|vinyl)?\s*album\b)")]
    private static partial Regex CountRx();

    // ---------- 4. our title words ----------
    private static readonly HashSet<string> Stop =
        ["the", "and", "og", "of", "de", "en", "et", "til", "på", "med", "for", "der", "det", "den", "fra", "nr", "vol", "co", "season",
         "sæson", "complete", "the", "dvd", "cd", "bog", "bind", "del", "part", "first", "second", "third", "fourth", "fifth", "sixth",
         "seventh", "eighth", "ninth", "tenth", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "series",
         "weeks", "uge", "edition", "udgave"];

    /// <summary>Rule 4: every real word of our own title (3+ letters, not a filler word) is in the listing.</summary>
    public static bool TitleWordsIn(Item i, string listing)
    {
        // A numbered blad is found by its series + number, not the story title (2026-11-24: "Kong Kylie 1949 Nr. 23" IS our
        // "Da Svend Asmussen ville være tandlæge"); the number itself is checked by NoIssueNumber.
        var text = FixedPrice.Magazines.Contains(i.Kind) && i.Number.Trim() != "" && i.Series.Trim() != "" ? i.Series : i.Title;
        var words = Words(text).Where(w => w.Length >= 3 && !Stop.Contains(w) && !w.All(char.IsDigit)).Distinct().ToList();
        if (words.Count == 0) return true;
        var theirs = Words(listing).ToHashSet();
        return words.All(theirs.Contains);
    }

    private static IEnumerable<string> Words(string text) =>
        WordRx().Matches(Plain(text)).Select(m => m.Value);

    /// <summary>Lower case without accents (é -> e) but with æ/ø/å kept, so "Café" and "Cafe" match.</summary>
    private static string Plain(string text)
    {
        var s = (text ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        foreach (var c in s) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    [GeneratedRegex(@"\b(?:sæson(?:er)?|seasons?|sesong|staffel|serie)\s*(?<list>\d+(?:\s*(?:-|–|,|og|and|&)\s*\d+)*)", RegexOptions.IgnoreCase)]
    private static partial Regex SeasonNumbersRx();

    [GeneratedRegex(@"(\d+)(?:\s*(?:-|–)\s*(\d+))?")]
    private static partial Regex RangeRx();

    [GeneratedRegex(@"\b(?<w>first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|første|anden|tredje|fjerde|femte)\s+(?:complete\s+)?(?:season|sæson)\b|\bseason\s+(?<w>one|two|three|four|five|six|seven|eight|nine|ten)\b", RegexOptions.IgnoreCase)]
    private static partial Regex OrdinalSeasonRx();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRx();
}
