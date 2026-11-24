namespace ITMartinDailyBrief.Domain;

// The readable text of one news article, fetched from the article's own page (user 2026-11-24: the summary was only
// from the headline, "not the article as a whole"). Short text = the page was locked (paywall) or could not be read,
// so the summary falls back to headline + teaser and the page says so.
public sealed record ArticleText(string Text)
{
    // 300: short real stories (DR breaking news ~300 chars) still count; empty/near-empty locked pages do not.
    public const int MinWhole = 300, MaxChars = 12000;

    public static readonly ArticleText None = new("");

    public bool IsWhole => Text.Length >= MinWhole;

    // Is this text about the story with this headline? Pages often show other stories too (DR, TV 2 Øst),
    // and a summary of the WRONG story is worse than none: at least a third of the headline's real words must be in the text.
    public static bool IsAbout(string text, string title)
    {
        var words = System.Text.RegularExpressions.Regex.Matches(title.ToLowerInvariant(), @"[\p{L}\p{N}]{4,}")
            .Select(m => m.Value).Distinct().ToList();
        if (words.Count == 0) return true;
        var lower = text.ToLowerInvariant();
        return words.Count(lower.Contains) * 3 >= words.Count;
    }

    public static ArticleText From(IEnumerable<string> paragraphs)
    {
        var text = string.Join("\n\n", paragraphs.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct());
        return new(text.Length > MaxChars ? text[..MaxChars] : text);
    }
}
