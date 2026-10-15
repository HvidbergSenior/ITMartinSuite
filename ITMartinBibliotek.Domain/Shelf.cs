using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ITMartinBibliotek.Domain;

// One album as a listener sees it. Jellyfin often splits a 2-cd set into two albums
// ("Up To Now [Disc 2]", or two albums with the very same name); those are joined here,
// and Parts keeps them in disc order so the player can run through all of them.
public sealed record Album(string Key, string Title, string Artist, int? Year, string CoverUrl, IReadOnlyList<MediaItem> Parts)
{
    // What the player asks for: the Jellyfin ids of the parts, in order.
    public string PlayId => string.Join(Shelf.PlayIdSeparator, Parts.Select(p => p.JellyfinId).Where(id => id != ""));
    public bool CanPlay => PlayId != "";
}

public sealed record Artist(string Name, string Key, string Letter, IReadOnlyList<Album> Albums)
{
    public string CoverUrl => Albums.Select(a => a.CoverUrl).FirstOrDefault(c => c != "") ?? "";
}

public sealed record SearchResult(IReadOnlyList<Artist> Artists, IReadOnlyList<Album> Albums, IReadOnlyList<MediaItem> Titles)
{
    public bool IsEmpty => Artists.Count == 0 && Albums.Count == 0 && Titles.Count == 0;
}

// How the shelf is laid out and searched: albums, artists A-Å, and one search over everything.
public static partial class Shelf
{
    public const char PlayIdSeparator = '_';
    public const string UnknownArtist = "Ukendt kunstner";
    public const string OtherLetter = "#";

    // "Echoes (Disc 1)", "Remixes 81-04 [Disc 2]", "Soundtrack [CD 1]", "The Greatest Hits CD2", "Live - Disk 1 af 2"
    [GeneratedRegex(@"\s*(?:[\(\[]\s*|-\s*|–\s*)?\b(?:disc|disk|cd)\s*(\d{1,2})(?:\s*(?:of|af)\s*\d{1,2})?\s*[\)\]]?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DiscMarker();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex NonWord();

    // "B-52's" and "B-52s" are the same band: apostrophes vanish instead of splitting the word.
    [GeneratedRegex(@"['’‘´`]")]
    private static partial Regex Apostrophe();

    // ("Up To Now", 2) for "Up To Now [Disc 2]"; (title, 0) when there is no disc marker.
    public static (string Title, int Disc) SplitDisc(string title)
    {
        var m = DiscMarker().Match(title);
        if (!m.Success || m.Index == 0) return (title.Trim(), 0);
        return (title[..m.Index].Trim(), int.Parse(m.Groups[1].Value));
    }

    // For matching and searching: lower case, accents off (é -> e) but æ, ø and å kept,
    // punctuation to spaces. "Bowie At The Beeb!" -> "bowie at the beeb".
    public static string Fold(string text)
    {
        // FormD would split å into a + ring and lose the ring, so it is parked first:
        // "Å" and "A" are different letters in Danish.
        const char parked = '';
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant().Normalize(NormalizationForm.FormC).Replace('å', parked).Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        var s = Apostrophe().Replace(sb.ToString().Replace(parked, 'å'), "");
        return NonWord().Replace(s, " ").Trim();
    }

    public static string ArtistName(MediaItem i) => string.IsNullOrWhiteSpace(i.Artist) ? UnknownArtist : i.Artist.Trim();

    // "The Beastie Boys" and "Beastie Boys" are one artist.
    public static string ArtistKey(string name) => SortName(name);

    public static string AlbumKey(MediaItem i) => ArtistKey(ArtistName(i)) + "|" + Fold(SplitDisc(i.Title).Title);

    // "The Beatles" sorts and files under B, like on a record shelf.
    private static string SortName(string name)
    {
        var f = Fold(name);
        return f.StartsWith("the ") ? f[4..] : f;
    }

    // Ordinal on a folded name puts æ, ø, å after z - the Danish alphabet - without
    // da-DK culture rules (which would also file "Aaron" under Å).
    private static readonly StringComparer Danish = StringComparer.Ordinal;

    public static string SortKey(string name) => SortName(name)
        .Replace('æ', ' ').Replace('ø', ' ').Replace('å', ' ');

    // A-Z, Æ, Ø, Å; digits and anything else under "#".
    public static string Letter(string name)
    {
        var s = SortName(name);
        if (s.Length == 0) return OtherLetter;
        var c = s[0];
        return c is >= 'a' and <= 'z' or 'æ' or 'ø' or 'å' ? char.ToUpperInvariant(c).ToString() : OtherLetter;
    }

    public static readonly IReadOnlyList<string> Alphabet =
        [.. Enumerable.Range('A', 26).Select(c => ((char)c).ToString()), "Æ", "Ø", "Å", OtherLetter];

    public static List<Album> Albums(IEnumerable<MediaItem> music) => music
        .GroupBy(AlbumKey)
        .Select(g =>
        {
            // Disc 1, 2, 3 in order; a part without a marker (the first cd of "X" + "X CD2") goes first.
            var parts = g.OrderBy(i => SplitDisc(i.Title).Disc).ThenBy(i => i.AddedAt).ToList();
            var first = parts[0];
            return new Album(g.Key, SplitDisc(first.Title).Title, ArtistName(first),
                parts.Select(p => p.Year).Where(y => y is not null).Min(),
                parts.Select(p => p.CoverUrl).FirstOrDefault(c => c != "") ?? "",
                parts);
        })
        .OrderBy(a => a.Year ?? 9999).ThenBy(a => SortKey(a.Title), Danish)
        .ToList();

    public static List<Artist> Artists(IEnumerable<Album> albums) => albums
        .GroupBy(a => ArtistKey(a.Artist))
        .Select(g =>
        {
            // The spelling most albums use ("AC/DC", not "Ac/Dc").
            var name = g.GroupBy(a => a.Artist).OrderByDescending(x => x.Count()).First().Key;
            return new Artist(name, g.Key, Letter(name), g.OrderBy(a => a.Year ?? 9999).ThenBy(a => SortKey(a.Title), Danish).ToList());
        })
        .OrderBy(a => a.Letter == OtherLetter ? 1 : 0).ThenBy(a => SortKey(a.Name), Danish)
        .ToList();

    // Every word of the query must be found; a name that starts with the query ranks first,
    // then one where a word starts with it, then anything containing it.
    private static int Rank(string folded, string q, string[] words)
    {
        if (!words.All(folded.Contains)) return -1;
        if (folded.StartsWith(q)) return 0;
        return (" " + folded).Contains(" " + words[0]) ? 1 : 2;
    }

    public static SearchResult Search(IReadOnlyList<MediaItem> items, string query, int max = 30)
    {
        var q = Fold(query);
        if (q.Length == 0) return new([], [], []);
        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var digits = new string(query.Where(char.IsDigit).ToArray());

        var albums = Albums(items.Where(i => i.Kind == MediaKind.Musik));
        var artists = Artists(albums)
            .Select(a => (a, r: Rank(Fold(a.Name), q, words)))
            .Where(x => x.r >= 0).OrderBy(x => x.r).ThenBy(x => SortKey(x.a.Name), Danish)
            .Select(x => x.a).Take(max).ToList();
        var albumHits = albums
            .Select(a => (a, r: Rank(Fold(a.Title), q, words) is var t and >= 0 ? t : Rank(Fold(a.Title + " " + a.Artist), q, words) is var c and >= 0 ? c + 3 : -1))
            .Where(x => x.r >= 0).OrderBy(x => x.r).ThenBy(x => SortKey(x.a.Title), Danish)
            .Select(x => x.a).Take(max).ToList();
        var titles = items.Where(i => i.Kind != MediaKind.Musik)
            .Select(i => (i, r: digits.Length >= 8 && i.Barcode == digits ? 0 : Rank(Fold(i.Title + " " + i.Collection), q, words)))
            .Where(x => x.r >= 0).OrderBy(x => x.r).ThenBy(x => SortKey(x.i.Title), Danish)
            .Select(x => x.i).Take(max).ToList();
        return new(artists, albumHits, titles);
    }
}
