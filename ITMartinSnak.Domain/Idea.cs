using System.Text.RegularExpressions;

namespace ITMartinSnak.Domain;

// Idéer (2026-11-20, user: "the snak room should have a chat and a ideer. Also a way to put different info from for instance
// email and websites into it easily"). Anyone in the room pastes a link or a piece of text (copied from a mail or a web page)
// and it becomes an idea card. Martin - or a named person in a shared room - can turn an idea into an Opgave (TaskId).
// The one who added it (same name) or Martin removes it. Ideas melt with the room like everything else.
public sealed record Idea(long Id, string Title, string Text, string Url, string By, DateTime At, long? TaskId = null);

public static partial class IdeaRules
{
    public const int MaxIdeas = 500, MaxTitle = 140, MaxText = 4000;

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)] private static partial Regex UrlShape();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();

    /// <summary>The first web address in the text, without the full stop or bracket a sentence puts after it.</summary>
    public static string? FirstUrl(string? text)
    {
        var m = UrlShape().Match(text ?? "");
        if (!m.Success) return null;
        var u = m.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}');
        return Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? u : null;
    }

    private static List<string> Lines(string? text) =>
        (text ?? "").Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    private static bool OnlyUrl(string line) => FirstUrl(line) is { } u && line.Trim().TrimEnd('.', ',') == u;

    /// <summary>Only a link was pasted (or the first line is too long to be a title) - then the page's own title is worth fetching.</summary>
    public static bool NeedsTitle(string? text) =>
        FirstUrl(text) is not null && Lines(text).FirstOrDefault(l => !OnlyUrl(l)) is not { Length: <= MaxTitle };

    public static string CleanTitle(string? title)
    {
        var t = Spaces().Replace(title ?? "", " ").Trim();
        if (t.Length <= MaxTitle) return t;
        var cut = t[..MaxTitle];
        var sp = cut.LastIndexOf(' ');
        return (sp > MaxTitle / 2 ? cut[..sp] : cut).TrimEnd() + "…";
    }

    /// <summary>"www.example.dk" for https://www.example.dk/side - the title when nothing better is known.</summary>
    public static string HostOf(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;

    /// <summary>
    /// What was pasted -> title, text and link. The first line that is not just the link is the title (a mail's subject, a
    /// page's heading); the rest is the text. Only a link -> the page's title, else the site's name.
    /// </summary>
    /// <returns>null when nothing was pasted.</returns>
    public static (string Title, string Text, string Url)? Parse(string? pasted, string? pageTitle = null)
    {
        var lines = Lines(pasted);
        if (lines.Count == 0) return null;
        var url = FirstUrl(pasted) ?? "";
        var words = lines.Where(l => !OnlyUrl(l)).ToList();
        var page = CleanTitle(pageTitle);
        string title, text;
        if (words.Count > 0 && words[0].Length <= MaxTitle)
        {
            title = words[0];
            text = string.Join("\n", words.Skip(1));
        }
        else
        {
            title = page.Length > 0 ? page : words.Count > 0 ? CleanTitle(words[0]) : HostOf(url);
            text = string.Join("\n", words);
        }
        if (text.Length > MaxText) text = text[..MaxText].TrimEnd() + "…";
        return (CleanTitle(title), text, url);
    }

    /// <returns>A Danish reason it cannot be added, or null.</returns>
    public static string? Check(string who, int existingCount)
    {
        if (who.Length == 0) return "Skriv dit navn først.";
        if (existingCount >= MaxIdeas) return "Der er for mange idéer i rummet. Slet nogle gamle først.";
        return null;
    }

    public static Idea New(IReadOnlyList<Idea> existing, (string Title, string Text, string Url) parsed, string by, DateTime nowUtc) =>
        new((existing.Count == 0 ? 0 : existing.Max(i => i.Id)) + 1, parsed.Title, parsed.Text, parsed.Url, by, nowUtc);

    /// <summary>Martin, or the one who added it (same name, case doesn't matter).</summary>
    public static bool MayDelete(Idea i, string? name, bool host) =>
        host || string.Equals(i.By, GroupRules.CleanName(name), StringComparison.OrdinalIgnoreCase);

    /// <summary>The Opgave's note: the idea's text and its link.</summary>
    public static string TaskNote(Idea i) => string.Join("\n", new[] { i.Text, i.Url }.Where(s => s.Length > 0));
}
