namespace ITMartinLager.Domain;

// Where on DBA and GulogGratis a kind of thing is sold (2026-11-16, user: "byg bedre prissøgning"). Both sites searched
// EVERY category, so "E-Man" found MAN trucks, "Vox" amplifiers, "X-Force" a Kawasaki and "Vigilante" a PS game - and
// their prices became our hints. Searching only the right category keeps them out before the title match even runs.
// Category ids read off the sites' own filter buttons 2026-11-16 (DBA may take several sub_category at once).
public static class MarketCategories
{
    private const string DbaBooks = "1.86.5209", DbaCollectibles = "1.86.285", DbaMusicFilm = "1.86.3922",
        DbaBoardGames = "1.86.5203", DbaConsoleGames = "1.93.3905";

    private static readonly string[] Paper = [Kinds.Bog, Kinds.Magasin, Kinds.Tegneserie, Kinds.Superhelte, Kinds.Jumbobog, Kinds.AndersAnd];
    private static readonly string[] Film = [Kinds.Dvd, Kinds.BluRay, Kinds.Vhs];
    private static readonly string[] Music = [Kinds.Cd, Kinds.Vinyl, Kinds.Kassette];
    private static readonly string[] Games = [Kinds.Braetspil, Kinds.Kortspil, Kinds.Magic];

    /// <summary>DBA's sub_category ids for the kind - none = every category (Merchandise, Andet ...).
    /// Old Anders And blade and comics are often put under Samlerobjekter, so paper searches both.</summary>
    public static string[] Dba(string kind) =>
        Paper.Contains(kind) ? [DbaBooks, DbaCollectibles]
        : Film.Contains(kind) || Music.Contains(kind) ? [DbaMusicFilm]
        : Games.Contains(kind) ? [DbaBoardGames, DbaCollectibles]
        : kind == Kinds.Rollespil ? [DbaBooks, DbaBoardGames]
        : kind == Kinds.Konsolspil ? [DbaConsoleGames]
        : [];

    /// <summary>"&amp;sub_category=1.86.5209&amp;sub_category=1.86.285" - to add to a DBA search address.</summary>
    public static string DbaQuery(string kind) => string.Concat(Dba(kind).Select(c => "&sub_category=" + c));

    /// <summary>GulogGratis' category path ("underholdning-og-laering/musik/") - "" = every category.
    /// One path only: books, blade and comics share "Underholdning &amp; læring".</summary>
    public static string GulogGratis(string kind) =>
        Paper.Contains(kind) || kind == Kinds.Rollespil ? "underholdning-og-laering/"
        : Film.Contains(kind) ? "underholdning-og-laering/film-og-serier/"
        : Music.Contains(kind) ? "underholdning-og-laering/musik/"
        : Games.Contains(kind) ? "sport-og-fritid/selskabsspil/"
        : kind == Kinds.Konsolspil ? "elektronik/spil-og-konsoller/"
        : "";
}
