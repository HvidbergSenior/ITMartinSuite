namespace ITMartinFaellesskab.Domain;

/// <summary>Where the "Måske kan du også lide" tips come from (free APIs, no keys). None = only Martin's and Rico's own tips.</summary>
public enum TipKind { None, Books, Magic, Music, PcGames }

/// <summary>One community. Activity = what members do together; NowLabel = the heading for "right now" (fx "Månedens bog");
/// StartQuery = what the tips search for until Martin sets a "right now".</summary>
public sealed record Community(string Slug, string Title, string Line, string Activity, string NowLabel, TipKind Tips, string StartQuery);

/// <summary>What the community is about right now, and when they meet - set by Martin (admin).</summary>
public sealed record NowItem(string Slug, string Title, string Note, string When, DateTime ChangedAt);

public sealed record Message(long Id, string Slug, string Name, string Text, DateTime At);

/// <summary>A customer (kundenummer) and the communities they have joined.</summary>
public sealed record Member(string Code, string Name, List<string> Slugs);

/// <summary>A suggestion from a free API (or Martin's own): what, one line, where to read more, a picture ("" = none).</summary>
public sealed record Tip(string Title, string Line, string Url, string Image);

// The communities (user 2026-10-29: "I want communities with books, anders and blade. etc. For nerds", "magic cards", "pc games",
// "board games", "remember the moviebox? ... Instead of renting, buy 5 DVDs", "also music"). Bogshoppen's interests lead here.
public static class CommunityRules
{
    public const int MaxMessagesPerCommunity = 2000, MaxText = 2000, MaxName = 30, MaxTitle = 120, MaxNote = 1000, MaxWhen = 80;

    public static readonly IReadOnlyList<Community> All =
    [
        new("boeger", "Bøger", "Læseklub for dem, der elsker fantasy, krimi og alt det andet.", "Læseklub: vi læser den samme bog og snakker om den.", "Månedens bog", TipKind.Books, "Terry Pratchett"),
        new("anders-and", "Anders And", "For samlere af Anders And & Co., Jumbobøger og Mammutbøger – gamle og nye.", "Vi sammenligner gamle og nye udgaver og hjælper hinanden med at finde de numre, vi mangler.", "Det kigger vi på", TipKind.None, ""),
        new("blade", "Blade og tegneserier", "Tintin, Asterix, Lucky Luke, Fantomet og superhelte fra DC og Marvel.", "Vi viser fund frem og snakker om serierne – gamle som nye.", "Månedens serie", TipKind.None, ""),
        new("magic", "Magic-kort", "For Magic: The Gathering-spillere og -samlere.", "Vi bytter, bygger decks og spiller sammen.", "Det spiller vi nu", TipKind.Magic, "Black Lotus"),
        new("braetspil", "Brætspil", "For dem, der elsker et godt brætspil rundt om bordet.", "Spilleaften: vi mødes og spiller månedens spil.", "Månedens spil", TipKind.None, ""),
        new("pc-spil", "PC-spil", "For dem, der spiller på computeren – nye spil og gamle klassikere.", "Vi spiller sammen online og giver hinanden tips.", "Det spiller vi nu", TipKind.PcGames, "Civilization"),
        new("videoaften", "Videoaften", "Kan du huske videoboksen? Fire film om fredagen og pizza i sofaen.", "Køb en kasse med 5 dvd'er, og se dem fredag aften – alene eller sammen med de andre.", "Ugens kasse", TipKind.None, ""),
        new("musik", "Musik", "For dem, der elsker cd'er og vinyl – og at lytte til en hel plade.", "Lytteaften: vi lytter til den samme plade og snakker om den.", "Månedens plade", TipKind.Music, "Gasolin'"),
    ];

    public static Community? Find(string? slug) => All.FirstOrDefault(c => string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public static string CleanName(string? name) => Cut((name ?? "").Trim(), MaxName);

    private static string Cut(string s, int max) => s.Length > max ? s[..max] : s;

    /// <summary>"4821 7730" -> "48217730"; null when it is not 8 digits.</summary>
    public static string? NormaliseCode(string? code)
    {
        var digits = new string((code ?? "").Where(char.IsDigit).ToArray());
        return digits.Length == 8 ? digits : null;
    }

    /// <returns>The new message, or null when the community, the name or the text is missing.</returns>
    public static Message? NewMessage(IReadOnlyList<Message> existing, string? slug, string? name, string? text, DateTime nowUtc)
    {
        var c = Find(slug);
        var n = CleanName(name);
        var t = Cut((text ?? "").Trim(), MaxText);
        if (c is null || n.Length == 0 || t.Length == 0) return null;
        return new Message((existing.Count == 0 ? 0 : existing.Max(m => m.Id)) + 1, c.Slug, n, t, nowUtc);
    }

    /// <summary>Only the newest <see cref="MaxMessagesPerCommunity"/> are kept in each community.</summary>
    public static void Trim(List<Message> messages, string slug)
    {
        var mine = messages.Where(m => m.Slug == slug).ToList();
        if (mine.Count <= MaxMessagesPerCommunity) return;
        var drop = mine.Take(mine.Count - MaxMessagesPerCommunity).Select(m => m.Id).ToHashSet();
        messages.RemoveAll(m => drop.Contains(m.Id));
    }

    /// <returns>The new "right now", or null when the community or the title is missing.</returns>
    public static NowItem? NewNow(string? slug, string? title, string? note, string? when, DateTime nowUtc)
    {
        var c = Find(slug);
        var ti = (title ?? "").Trim();
        if (c is null || ti.Length == 0) return null;
        return new NowItem(c.Slug, Cut(ti, MaxTitle), Cut((note ?? "").Trim(), MaxNote), Cut((when ?? "").Trim(), MaxWhen), nowUtc);
    }

    /// <summary>Joins the community, or leaves it again if already in it.</summary>
    public static Member? ToggleJoin(Member m, string? slug)
    {
        if (Find(slug) is not { } c) return null;
        var slugs = m.Slugs.ToList();
        if (!slugs.Remove(c.Slug)) slugs.Add(c.Slug);
        return m with { Slugs = slugs };
    }

    /// <summary>What the tips search for: the "right now" title, else the community's start query ("" = no automatic tips).</summary>
    public static string TipQuery(Community c, NowItem? now) =>
        c.Tips == TipKind.None ? "" : now is { Title.Length: > 0 } ? now.Title : c.StartQuery;
}
