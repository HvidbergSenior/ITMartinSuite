namespace ITMartinSnak.Domain;

// A group's people and topics (2026-11-09, user: replace the Facebook group of Acceleratorkursus Hold 36). Members = the
// course's kursistoversigt (name + what they work with): the door shows the names, so people tap theirs instead of typing.
// Topics = the workshops: a file is put under one, and the Filer tab is grouped by them. Martin sets both.
public sealed record Member(string Name, string About);

public static class RosterRules
{
    public const int MaxMembers = 100, MaxAbout = 200, MaxTopics = 30, MaxTopic = 60;
    public const string OtherTopic = "Andet";

    private static readonly string[] Separators = [" – ", " — ", " - ", "\t", "|", ":"];

    /// <summary>One member per line: "Navn – hvad de laver" (also "-", "|", ":" or a tab). Duplicates and blank lines go.</summary>
    public static List<Member> ParseMembers(string? text)
    {
        var list = new List<Member>();
        foreach (var raw in Lines(text))
        {
            // The earliest separator wins, so a dash inside the description stays there.
            var (name, about) = (raw, "");
            var cut = Separators.Select(s => (At: raw.IndexOf(s, StringComparison.Ordinal), Len: s.Length))
                .Where(x => x.At > 0).OrderBy(x => x.At).FirstOrDefault();
            if (cut.Len > 0) { name = raw[..cut.At]; about = raw[(cut.At + cut.Len)..]; }
            name = GroupRules.CleanName(name);
            about = about.Trim();
            if (about.Length > MaxAbout) about = about[..MaxAbout];
            if (name.Length == 0 || list.Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(new Member(name, about));
            if (list.Count == MaxMembers) break;
        }
        return list;
    }

    /// <summary>One topic per line, in the order given; "Andet" is always there at the end, so no file is left without one.</summary>
    public static List<string> ParseTopics(string? text)
    {
        var list = new List<string>();
        foreach (var raw in Lines(text))
        {
            var t = raw.Length > MaxTopic ? raw[..MaxTopic] : raw;
            if (string.Equals(t, OtherTopic, StringComparison.OrdinalIgnoreCase) || list.Contains(t, StringComparer.OrdinalIgnoreCase)) continue;
            list.Add(t);
            if (list.Count == MaxTopics) break;
        }
        return list;
    }

    /// <summary>The topic a file goes under: one of the room's topics (matched without case), else "" (= Andet).</summary>
    public static string TopicFor(IReadOnlyList<string>? topics, string? asked)
    {
        var a = (asked ?? "").Trim();
        return topics?.FirstOrDefault(t => string.Equals(t, a, StringComparison.OrdinalIgnoreCase)) ?? "";
    }

    public static string MembersText(IEnumerable<Member>? members) =>
        string.Join("\n", (members ?? []).Select(m => m.About.Length > 0 ? $"{m.Name} – {m.About}" : m.Name));

    private static IEnumerable<string> Lines(string? text) =>
        (text ?? "").Split('\n').Select(l => l.Trim().TrimStart('•', '*').Trim()).Where(l => l.Length > 0);
}
