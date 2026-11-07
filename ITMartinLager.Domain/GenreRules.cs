using System.Text.RegularExpressions;

namespace ITMartinLager.Domain;

/// <summary>Which genre (Genres) a thing is, from the tags a free source gives (user 2026-11-07: "like danktop, indie, pop. and
/// on the other shops as well"). Music: MusicBrainz tags, most votes first, plus "country:DK" for a Danish artist. Film: TMDb's
/// genre names in TMDb's order. Books: Open Library subjects (no order). Plain rules, no AI; "" = none of the list fits.</summary>
public static partial class GenreRules
{
    /// <summary>The pseudo-tag a music source adds for an artist from Denmark.</summary>
    public const string DanishArtist = "country:DK";

    public static string Pick(string category, IReadOnlyList<string> tags, string title = "") => category switch
    {
        ProductTypes.Musik => Music(Clean(tags)),
        ProductTypes.Film => Film(Clean(tags), title),
        ProductTypes.Boeger => Book(Clean(tags)),
        _ => "",
    };

    private static List<string> Clean(IReadOnlyList<string> tags) =>
        tags.Select(t => (t ?? "").Trim().ToLowerInvariant()).Where(t => t != "").ToList();

    // A keyword is a whole word (or words) inside the tag: "vise" is not "improvised", "rock" is in "pop rock".
    private static bool Has(string tag, string word) =>
        tag == word || Regex.IsMatch(tag, @"(^|[^\p{L}])" + Regex.Escape(word) + @"($|[^\p{L}])");

    private static bool Any(IEnumerable<string> tags, params string[] words) => tags.Any(t => words.Any(w => Has(t, w)));

    // ----- Music -----

    // One tag -> a genre, the most telling word first ("pop rock" = Rock, "indie pop" = Indie, "folk rock" = Folk).
    private static readonly (string Genre, string[] Words)[] MusicWords =
    [
        ("Klassisk", ["classical", "klassisk", "baroque", "opera", "symphony", "chamber music", "romantic classical"]),
        ("Jazz", ["jazz", "swing", "bebop", "big band", "bossa nova"]),
        ("Metal", ["metal", "heavy metal", "death metal", "black metal", "thrash"]),
        ("Hip hop", ["hip hop", "hip-hop", "hiphop", "rap", "trap"]),
        ("Elektronisk", ["electronic", "electronica", "techno", "house", "trance", "edm", "ambient", "drum and bass", "dubstep", "idm"]),
        ("Soul og R&B", ["soul", "r&b", "rnb", "rhythm and blues", "funk", "motown", "gospel", "neo soul"]),
        ("Country", ["country", "bluegrass", "americana"]),
        ("Indie", ["indie", "alternative", "shoegaze", "lo-fi"]),
        ("Blues", ["blues"]),
        ("Folk", ["folk", "singer-songwriter", "singer/songwriter", "acoustic", "celtic"]),
        ("Rock", ["rock", "punk", "grunge", "hard rock", "new wave", "post-punk"]),
        ("Pop", ["pop", "synthpop", "synth-pop", "europop", "disco", "dance-pop", "dance", "teen pop"]),
    ];

    private static string Music(List<string> tags)
    {
        // These win wherever they are: a children's record or a soundtrack is that first.
        if (Any(tags, "children's music", "children", "kids", "børnemusik", "børnesange", "nursery rhymes")) return "Børnemusik";
        if (Any(tags, "soundtrack", "film score", "score", "musical", "filmmusik", "show tunes")) return "Soundtrack";
        if (Any(tags, "dansktop", "dansk top", "schlager", "danseband", "dansk-top")) return "Dansktop";
        if (Any(tags, "visesang", "viser", "vise", "danske viser")) return "Visesang";
        var danish = tags.Contains(DanishArtist.ToLowerInvariant());
        var genre = "";
        foreach (var t in tags.Where(t => t != DanishArtist.ToLowerInvariant()))
            if (MusicWords.FirstOrDefault(m => m.Words.Any(w => Has(t, w))).Genre is { } g) { genre = g; break; }
        // A Danish artist: easy listening and country is dansktop, folk and singer-songwriter is visesang.
        if (danish && (genre == "Country" || Any(tags, "easy listening"))) return "Dansktop";
        if (danish && genre == "Folk") return "Visesang";
        if (genre == "" && Any(tags, "easy listening")) return "Pop";
        return genre;
    }

    // ----- Film -----

    private static readonly (string Genre, string[] Words)[] FilmWords =
    [
        ("Action", ["action", "adventure", "action & adventure"]),
        ("Komedie", ["comedy", "komedie"]),
        ("Krimi", ["crime", "mystery", "krimi"]),
        ("Gys", ["horror", "gyser", "gys"]),
        ("Thriller", ["thriller"]),
        ("Sci-fi", ["science fiction", "sci-fi", "sci-fi & fantasy"]),
        ("Fantasy", ["fantasy"]),
        ("Romantik", ["romance", "romantik"]),
        ("Krig", ["war", "war & politics", "krig"]),
        ("Western", ["western"]),
        ("Drama", ["drama", "history", "soap"]),
    ];

    private static string Film(List<string> tags, string title)
    {
        var t0 = title.ToLowerInvariant();
        if (Any(tags, "stand-up", "standup") || Has(t0, "stand-up") || Has(t0, "standup") || Has(t0, "stand up")) return "Stand-up";
        if (Any(tags, "documentary", "dokumentar")) return "Dokumentar";
        if (Any(tags, "family", "kids", "børnefilm")) return "Børnefilm";
        if (Any(tags, "animation")) return "Animation";
        foreach (var t in tags)
            if (FilmWords.FirstOrDefault(f => f.Words.Any(w => Has(t, w))).Genre is { } g) return g;
        return "";
    }

    // ----- Books -----

    // Open Library subjects have no order, so the most telling genre found anywhere wins.
    private static readonly (string Genre, string[] Words)[] BookWords =
    [
        ("Børnebøger", ["juvenile fiction", "juvenile literature", "juvenile", "children's fiction", "children's stories", "children's literature", "picture books", "børnebøger"]),
        ("Biografi", ["biography", "autobiography", "memoirs", "memoir", "biografi", "erindringer"]),
        ("Fantasy", ["fantasy", "fantasy fiction", "magic"]),
        ("Sci-fi", ["science fiction"]),
        ("Gys", ["horror", "horror fiction", "ghost stories", "horror tales", "gys"]),
        ("Krimi", ["detective and mystery stories", "detective", "mystery", "crime", "murder", "thriller", "thrillers", "spy stories", "krimi", "kriminalromaner"]),
        ("Kærlighed", ["romance", "love stories", "romance fiction", "kærlighed"]),
        ("Humor", ["humor", "humour", "wit and humor", "humorous stories", "humoristiske"]),
        ("Romaner", ["fiction", "novel", "novels", "roman", "romaner", "danish fiction", "skønlitteratur"]),
    ];

    private static string Book(List<string> tags)
    {
        if (tags.Count == 0) return "";
        foreach (var (genre, words) in BookWords)
            if (Any(tags, words)) return genre;
        return "Fagbøger";   // subjects, but none of a story: history, cooking, travel ...
    }
}
