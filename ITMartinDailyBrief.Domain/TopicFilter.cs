using System.Text.RegularExpressions;

namespace ITMartinDailyBrief.Domain;

// "Kun nyheder om ..." (user 2026-11-24: "choose which news I want, like Donald Trump").
// The reader writes topics separated by comma or new line. A news item matches a topic when EVERY word of the topic
// is in its title or text, as the start of a word, ignoring case: "Trump" matches "Trumps" and "Trump-regeringen".
// For a name of several words the last word alone is enough ("Donald Trump" also matches "Trump siger ..."),
// because the news often only uses the surname. No topics = everything. Plain rules, no AI.
public sealed class TopicFilter
{
    public const int MaxTopics = 10, MaxLength = 40;

    public IReadOnlyList<string> Topics { get; }

    public TopicFilter(IEnumerable<string>? topics = null) =>
        Topics = (topics ?? [])
            .Select(t => Regex.Replace(t ?? "", @"\s+", " ").Trim())
            .Where(t => t.Length > 0)
            .Select(t => t.Length > MaxLength ? t[..MaxLength].Trim() : t)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTopics)
            .ToList();

    public static TopicFilter Parse(string? text) => new((text ?? "").Split([',', '\n', ';'], StringSplitOptions.RemoveEmptyEntries));

    public bool IsEmpty => Topics.Count == 0;
    public string AsText => string.Join(", ", Topics);

    public bool Matches(NewsItem item)
    {
        if (IsEmpty) return true;
        var text = item.Title + " " + item.Description;
        return Topics.Any(t => Matches(text, t));
    }

    private static bool Matches(string text, string topic)
    {
        var words = topic.Split(' ');
        if (words.All(w => HasWord(text, w))) return true;
        return words.Length > 1 && words[^1].Length >= 3 && HasWord(text, words[^1]);
    }

    // Start of a word: "Trump" in "Trumps" yes, in "Atrump" no.
    private static bool HasWord(string text, string word) =>
        Regex.IsMatch(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(word), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
