using System.Text.RegularExpressions;

namespace ITMartinLager.Domain;

/// <summary>Which genre (Genres) a thing is (user 2026-11-07: "like danktop, indie, pop. and on the other shops as well", then
/// "i need as many genres for all the shops - krimi, etc?"). Music: MusicBrainz tags, most votes first, plus "country:DK" for a
/// Danish artist. Film: TMDb's genre names in TMDb's order. Books: Open Library subjects (no order). Comics and games: the series
/// and title (no free source knows Danish blade). Plain rules, no AI; "" = none of the list fits.</summary>
public static partial class GenreRules
{
    /// <summary>The pseudo-tag a music source adds for an artist from Denmark.</summary>
    public const string DanishArtist = "country:DK";

    private static readonly string[] ComicCategories = [ProductTypes.Tegneserier, ProductTypes.Serieblade, ProductTypes.GamleBlade,
        ProductTypes.Jumbo, ProductTypes.Superhelte];

    /// <summary>Things whose genre comes from their own name - no lookup needed.</summary>
    public static bool FromName(string category) => ComicCategories.Contains(category)
        || category is ProductTypes.Computerspil or ProductTypes.Braetspil or ProductTypes.Rollespil;

    public static string Pick(string category, IReadOnlyList<string> tags, string title = "", string series = "") => category switch
    {
        ProductTypes.Musik => Music(Clean(tags)),
        ProductTypes.Film => Film(Clean(tags), title),
        ProductTypes.Boeger => Book(Clean(tags)),
        ProductTypes.Computerspil => ByName(GameWords, title, series),
        ProductTypes.Braetspil => ByName(BoardWords, title, series),
        ProductTypes.Rollespil => ByName(RoleplayWords, title, series) is { Length: > 0 } r ? r : "Andre rollespil",
        _ when ComicCategories.Contains(category) => Comic(category, title, series),
        _ => "",
    };

    private static List<string> Clean(IReadOnlyList<string> tags) =>
        tags.Select(t => (t ?? "").Trim().ToLowerInvariant()).Where(t => t != "").ToList();

    // A keyword is a whole word (or words) inside the tag: "vise" is not "improvised", "rock" is in "pop rock".
    private static bool Has(string tag, string word) =>
        tag == word || Regex.IsMatch(tag, @"(^|[^\p{L}\p{N}])" + Regex.Escape(word) + @"($|[^\p{L}\p{N}])");

    private static bool Any(IEnumerable<string> tags, params string[] words) => tags.Any(t => words.Any(w => Has(t, w)));

    private static string? First((string Genre, string[] Words)[] table, string tag) =>
        table.FirstOrDefault(m => m.Words.Any(w => Has(tag, w))).Genre;

    // ----- Music -----

    // One tag -> a genre, the most telling word first ("pop rock" = Rock, "indie pop" = Indie, "folk rock" = Folk, "punk rock" = Punk).
    private static readonly (string Genre, string[] Words)[] MusicWords =
    [
        ("Klassisk", ["classical", "klassisk", "baroque", "opera", "symphony", "chamber music", "romantic classical"]),
        ("Jazz", ["jazz", "swing", "bebop", "big band", "bossa nova"]),
        ("Metal", ["metal", "heavy metal", "death metal", "black metal", "thrash"]),
        ("Punk", ["punk", "punk rock", "hardcore punk", "post-punk", "pop punk"]),
        ("Hip hop", ["hip hop", "hip-hop", "hiphop", "rap", "trap"]),
        ("Reggae", ["reggae", "ska", "dub", "dancehall", "rocksteady"]),
        ("Latin", ["latin", "salsa", "tango", "samba", "latin pop", "reggaeton", "flamenco"]),
        ("Gospel", ["gospel", "christian", "kristen", "salmer", "hymns", "worship"]),
        ("Verdensmusik", ["world", "world music", "african", "afrobeat", "celtic", "klezmer", "fado", "bhangra"]),
        ("Disco", ["disco", "nu-disco", "italo disco"]),
        ("Elektronisk", ["electronic", "electronica", "techno", "house", "trance", "edm", "ambient", "drum and bass", "dubstep", "idm"]),
        ("Soul og R&B", ["soul", "r&b", "rnb", "rhythm and blues", "funk", "motown", "neo soul"]),
        ("Country", ["country", "bluegrass", "americana"]),
        ("Indie", ["indie", "alternative", "shoegaze", "lo-fi"]),
        ("Blues", ["blues"]),
        ("Folk", ["folk", "singer-songwriter", "singer/songwriter", "acoustic"]),
        ("Rock", ["rock", "grunge", "hard rock", "new wave"]),
        ("Pop", ["pop", "synthpop", "synth-pop", "europop", "dance-pop", "dance", "teen pop"]),
    ];

    private static string Music(List<string> tags)
    {
        // These win wherever they are: a children's record, a Christmas record or a soundtrack is that first.
        if (Any(tags, "children's music", "children", "kids", "børnemusik", "børnesange", "nursery rhymes")) return "Børnemusik";
        if (Any(tags, "christmas", "christmas music", "jul", "julemusik", "julesange", "xmas")) return "Julemusik";
        if (Any(tags, "soundtrack", "film score", "score", "musical", "filmmusik", "show tunes")) return "Soundtrack";
        if (Any(tags, "dansktop", "dansk top", "schlager", "danseband", "dansk-top")) return "Dansktop";
        if (Any(tags, "visesang", "viser", "vise", "danske viser")) return "Visesang";
        var danish = tags.Contains(DanishArtist.ToLowerInvariant());
        var genre = "";
        foreach (var t in tags.Where(t => t != DanishArtist.ToLowerInvariant()))
            if (First(MusicWords, t) is { } g) { genre = g; break; }
        // A Danish artist: easy listening and country is dansktop, folk and singer-songwriter is visesang.
        if (danish && (genre == "Country" || Any(tags, "easy listening"))) return "Dansktop";
        if (danish && genre == "Folk") return "Visesang";
        if (genre == "" && Any(tags, "easy listening")) return "Pop";
        return genre;
    }

    // ----- Film -----

    private static readonly (string Genre, string[] Words)[] FilmWords =
    [
        ("Action", ["action", "action & adventure"]),
        ("Eventyr", ["adventure"]),
        ("Komedie", ["comedy", "komedie"]),
        ("Krimi", ["crime", "mystery", "krimi"]),
        ("Gys", ["horror", "gyser", "gys"]),
        ("Thriller", ["thriller"]),
        ("Sci-fi", ["science fiction", "sci-fi", "sci-fi & fantasy"]),
        ("Fantasy", ["fantasy"]),
        ("Romantik", ["romance", "romantik"]),
        ("Krig", ["war", "war & politics", "krig"]),
        ("Western", ["western"]),
        ("Musical", ["music", "musical"]),
        ("Historie", ["history"]),
        ("Drama", ["drama", "soap"]),
    ];

    private static string Film(List<string> tags, string title)
    {
        var t0 = title.ToLowerInvariant();
        if (Any(tags, "stand-up", "standup") || Has(t0, "stand-up") || Has(t0, "standup") || Has(t0, "stand up")) return "Stand-up";
        if (Any(tags, "documentary", "dokumentar")) return "Dokumentar";
        if (Any(tags, "family", "kids", "børnefilm")) return "Børnefilm";
        if (Any(tags, "animation")) return "Animation";
        foreach (var t in tags)
            if (First(FilmWords, t) is { } g) return g;
        return "";
    }

    // ----- Books -----

    // Open Library subjects have no order, so the most telling genre found anywhere wins (children's and young adult first,
    // then the story genres, then the kinds of fagbog).
    private static readonly (string Genre, string[] Words)[] BookWords =
    [
        ("Børnebøger", ["juvenile fiction", "juvenile literature", "children's fiction", "children's stories", "children's literature", "picture books", "børnebøger"]),
        ("Ungdomsbøger", ["young adult fiction", "young adult", "teenagers", "ungdomsbøger", "ungdomsroman"]),
        ("Biografi", ["biography", "autobiography", "memoirs", "memoir", "biografi", "erindringer"]),
        ("Digte", ["poetry", "poems", "digte", "lyrik"]),
        ("Fantasy", ["fantasy", "fantasy fiction"]),
        ("Sci-fi", ["science fiction"]),
        ("Gys", ["horror", "horror fiction", "ghost stories", "horror tales", "gys"]),
        ("Krimi", ["detective and mystery stories", "detective", "mystery", "crime", "murder", "krimi", "kriminalromaner", "police"]),
        ("Thriller", ["thriller", "thrillers", "spy stories", "suspense", "espionage"]),
        ("Kærlighed", ["romance", "love stories", "romance fiction", "kærlighed"]),
        ("Eventyr", ["adventure stories", "adventure", "fairy tales", "eventyr", "sea stories"]),
        ("Humor", ["humor", "humour", "wit and humor", "humorous stories", "humoristiske"]),
        ("Romaner", ["fiction", "novel", "novels", "roman", "romaner", "danish fiction", "skønlitteratur"]),
        ("Kogebøger", ["cooking", "cookery", "recipes", "kogebøger", "baking", "food"]),
        ("Krig", ["world war, 1939-1945", "world war, 1914-1918", "military history", "war", "krig"]),
        ("Historie", ["history", "historie", "archaeology", "civilization"]),
        ("Rejser", ["travel", "description and travel", "guidebooks", "rejser"]),
        ("Natur og dyr", ["nature", "animals", "birds", "plants", "natural history", "gardening", "dogs", "cats"]),
        ("Sport", ["sports", "football", "soccer", "sport", "cycling", "fishing"]),
        ("Kunst og musik", ["art", "music", "painting", "photography", "architecture", "design", "musicians"]),
        ("Religion og filosofi", ["religion", "philosophy", "christianity", "bible", "theology", "spirituality"]),
        ("Psykologi og selvhjælp", ["psychology", "self-help", "self-actualization", "personal development", "selvhjælp", "parenting"]),
        ("Hobby", ["handicraft", "crafts", "knitting", "sewing", "model", "collectors and collecting", "games", "hobbies"]),
    ];

    private static string Book(List<string> tags)
    {
        if (tags.Count == 0) return "";
        foreach (var (genre, words) in BookWords)
            if (Any(tags, words)) return genre;
        return "Fagbøger";   // subjects, but none of the above: law, economy, computers ...
    }

    // ----- Comics (from the series, then the title) -----

    private static readonly (string Genre, string[] Words)[] ComicWords =
    [
        ("Disney", ["anders and", "walt disney", "disney", "mickey", "andeby", "onkel joakim", "fedtmule", "supermule", "jumbobog",
            "mammutbog", "anders and ekstra", "sommer-sjov", "rip, rap og rup", "bedstemor and", "dumbo", "donald duck"]),
        ("Western", ["jonah hex", "lucky luke", "blueberry", "comanche", "tex willer", "buddy longway", "jerry spring", "red ryder",
            "western", "bessy", "cisco kid"]),
        ("Superhelte", ["spider-man", "spiderman", "edderkoppen", "batman", "superman", "x-men", "fantastiske 4", "fantastic four",
            "projekt x", "avengers", "hulk", "legionnaires", "freak force", "dv8", "gambit", "elementals", "freex", "spawn",
            "savage dragon", "wildc.a.t.s", "e-man", "green hornet", "flinch", "daredevil", "wonder woman", "justice league", "captain america",
            "iron man", "thor", "silver surfer", "wolverine", "punisher", "ultimate"]),
        ("Gys", ["tales from the crypt", "vampirella", "hellboy", "gyser", "horror", "creepy", "eerie", "the walking dead", "zombie"]),
        ("Krig", ["commando", "kommando", "krigsret", "sgt. rock", "sergent", "war", "krigs", "battle"]),
        ("Krimi", ["agent 327", "ric hochet", "xiii", "largo winch", "modesty blaise", "grendel", "dick tracy", "sin city", "detektiv",
            "kriminal", "spirit"]),
        ("Sci-fi", ["valerian", "storm", "judge dredd", "star wars", "star trek", "flash gordon", "dan dare", "rael", "yoko tsuno",
            "blake og mortimer", "aliens"]),
        ("Fantasy", ["thorgal", "conan", "elfquest", "red sonja", "dragonlance", "lanfeust", "fantasy"]),
        ("Underground", ["underground", "komix", "freak brothers", "fritz the cat", "zap", "crumb", "supermuld"]),
        ("Eventyr", ["tintin", "fantomet", "spirou", "splint", "tarzan", "prins valiant", "vovehalsene", "corto maltese", "bob morane",
            "johan og trille", "rasmus klump", "smølferne", "flipper", "bummelum"]),
        ("Humor", ["basserne", "garfield", "haha", "kong kylie", "skipper skræk", "asterix", "gaston", "hagbard", "radiserne",
            "familien gnuff", "momsemor", "lille lasse", "jonathan", "birger og viola", "steen og stoffer", "ernie", "rocky", "zits",
            "calvin", "carlo", "humor", "sjov", "striber", "dennis", "nemi"]),
    ];

    private static string Comic(string category, string title, string series)
    {
        var name = $"{series} {title}".ToLowerInvariant();
        if (First(ComicWords, name) is { } g) return g;
        return category == ProductTypes.Superhelte ? "Superhelte" : "";
    }

    // ----- Games (from the title) -----

    private static readonly (string Genre, string[] Words)[] GameWords =
    [
        ("Sport", ["fifa", "nhl", "nba", "pes", "pro evolution", "madden", "tennis", "golf", "football manager", "skate", "tony hawk", "sports", "olympic"]),
        ("Racing", ["need for speed", "gran turismo", "forza", "racing", "rally", "f1", "formula", "mario kart", "burnout", "crash team"]),
        ("Skydespil", ["call of duty", "battlefield", "halo", "doom", "counter-strike", "medal of honor", "far cry", "bioshock", "rainbow six", "quake", "wolfenstein"]),
        ("Rollespil", ["final fantasy", "elder scrolls", "skyrim", "oblivion", "witcher", "diablo", "pokemon", "pokémon", "mass effect", "fallout", "baldur", "dragon age"]),
        ("Strategi", ["civilization", "age of empires", "command & conquer", "total war", "starcraft", "warcraft", "galactic civilizations", "heroes of might", "anno", "settlers"]),
        ("Simulation", ["sims", "simulator", "flight", "farming", "simcity", "cities", "tycoon", "planet coaster"]),
        ("Familie", ["lego", "singstar", "buzz", "just dance", "wii sports", "party", "kinect", "minecraft"]),
        ("Platform", ["mario", "sonic", "crash bandicoot", "spyro", "rayman", "ratchet", "jak and daxter", "donkey kong", "kirby"]),
        ("Eventyr", ["tomb raider", "uncharted", "zelda", "monkey island", "broken sword", "myst", "adventure", "eventyr"]),
        ("Action", ["grand theft auto", "gta", "assassin", "god of war", "devil may cry", "batman", "spider-man", "metal gear", "hitman", "action"]),
    ];

    private static readonly (string Genre, string[] Words)[] BoardWords =
    [
        ("Quiz", ["trivial pursuit", "bezzerwizzer", "quiz", "hitster", "vild med"]),
        ("Samarbejde", ["pandemic", "forbidden island", "samarbejde", "co-op", "spirit island", "hanabi"]),
        ("Kortspil", ["uno", "kortspil", "poker", "skip-bo", "phase 10", "dobble", "exploding kittens", "cards"]),
        ("Børnespil", ["børn", "junior", "kids", "memory", "stigespil", "fang", "fisk"]),
        ("Selskabsspil", ["alias", "pictionary", "partners", "cluedo", "dixit", "codenames", "jungle speed", "werewolf", "varulv", "selskabsspil", "party"]),
        ("Strategispil", ["catan", "carcassonne", "risk", "ticket to ride", "skak", "chess", "axis & allies", "terraforming", "agricola", "diplomacy", "stratego"]),
        ("Familiespil", ["ludo", "matador", "monopoly", "yatzy", "scrabble", "rummikub", "kalaha", "familie", "familiespil"]),
    ];

    private static readonly (string Genre, string[] Words)[] RoleplayWords =
    [
        ("Dungeons & Dragons", ["dungeons & dragons", "dungeons and dragons", "d&d", "ad&d", "forgotten realms", "dragonlance", "greyhawk", "ravenloft", "eberron"]),
        ("Pathfinder", ["pathfinder", "starfinder"]),
        ("Warhammer", ["warhammer", "40k", "40,000", "age of sigmar", "wfrp"]),
        ("Call of Cthulhu", ["call of cthulhu", "cthulhu", "chaosium"]),
    ];

    private static string ByName((string Genre, string[] Words)[] table, string title, string series) =>
        First(table, $"{series} {title}".ToLowerInvariant()) ?? "";
}
