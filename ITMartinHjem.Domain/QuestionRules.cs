using System.Text.RegularExpressions;

namespace ITMartinHjem.Domain;

// The rules behind "Det spørger folk om": no names or contact details in public, a topic on every question,
// and search + "flere om det samme" so people find the other questions about the same thing (user 2026-10-12).
public static partial class QuestionRules
{
    public const string NoTopic = "Andet";
    public const string ITMartin = "itmartin", Bogshoppen = "bogshoppen";

    public static string NormalizeSite(string? site) =>
        string.Equals(site?.Trim(), Bogshoppen, StringComparison.OrdinalIgnoreCase) ? Bogshoppen : ITMartin;

    public static IEnumerable<PublicQuestion> OnSite(IEnumerable<PublicQuestion> all, string? site) =>
        site is null ? all : all.Where(q => q.Site == NormalizeSite(site));

    [GeneratedRegex(@"[\w.+-]+@[\w-]+(\.[\w-]+)+")] private static partial Regex Email();
    [GeneratedRegex(@"(\+45[\s-]?)?\b\d{2}[\s-]?\d{2}[\s-]?\d{2}[\s-]?\d{2}\b")] private static partial Regex Phone();
    [GeneratedRegex(@"[ \t]{2,}")] private static partial Regex Spaces();
    [GeneratedRegex(@"[^\p{L}\p{N}]+")] private static partial Regex NonWord();

    // Removes what identifies the visitor: e-mail addresses, Danish phone numbers and the name they gave in the chat.
    // Martin still reads it before it goes up - a name inside the text ("min søn Peter") is his call.
    public static string Anonymize(string text, params string?[] names)
    {
        var t = Email().Replace(text ?? "", "[skjult]");
        t = Phone().Replace(t, "[skjult]");
        foreach (var n in names)
        {
            if (string.IsNullOrWhiteSpace(n) || n.Trim().Length < 2) continue;
            t = Regex.Replace(t, $@"\b{Regex.Escape(n.Trim())}\b", "", RegexOptions.IgnoreCase);
        }
        return string.Join("\n", t.Split('\n').Select(l => Spaces().Replace(l, " ").Trim())).Trim();
    }

    // The visitor's first message(s) = the question, Martin's replies = the answer. Both anonymised.
    public static (string Question, string Answer) DraftFromChat(IEnumerable<(bool FromOwner, string Text)> messages, string? visitorName)
    {
        var list = messages.ToList();
        var question = string.Join("\n", list.TakeWhile(m => !m.FromOwner).Select(m => m.Text.Trim()).Where(t => t.Length > 0));
        if (question.Length == 0) question = string.Join("\n", list.Where(m => !m.FromOwner).Select(m => m.Text.Trim()).Where(t => t.Length > 0));
        var answer = string.Join("\n", list.Where(m => m.FromOwner).Select(m => m.Text.Trim()).Where(t => t.Length > 0));
        return (Anonymize(question, visitorName), Anonymize(answer, visitorName));
    }

    public static string NormalizeTopic(string? topic)
    {
        var t = Spaces().Replace((topic ?? "").Trim(), " ");
        if (t.Length == 0) return NoTopic;
        if (t.Length > PublicQuestion.MaxTopic) t = t[..PublicQuestion.MaxTopic].Trim();
        return char.ToUpperInvariant(t[0]) + t[1..];
    }

    // Topics with how many questions each, most asked first - the chips on the page.
    public static List<(string Topic, int Count)> Topics(IEnumerable<PublicQuestion> all) =>
        all.GroupBy(q => q.Topic, StringComparer.OrdinalIgnoreCase)
           .Select(g => (g.First().Topic, g.Count()))
           .OrderByDescending(x => x.Item2).ThenBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
           .ToList();

    // Every word must be found in the question, the answer or the topic; hits in the question rank higher.
    public static List<PublicQuestion> Search(IEnumerable<PublicQuestion> all, string? query, string? topic)
    {
        var words = Words(query ?? "").ToList();
        return all
            .Where(q => string.IsNullOrWhiteSpace(topic) || string.Equals(q.Topic, topic.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(q => (q, score: Score(q, words)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score).ThenByDescending(x => x.q.PublishedAt)
            .Select(x => x.q)
            .ToList();
    }

    // "Flere spørgsmål om det samme": same topic first, then questions that share real words with this one.
    public static List<PublicQuestion> Related(PublicQuestion q, IEnumerable<PublicQuestion> all, int max = 5)
    {
        var mine = Words(q.Question + " " + q.Topic).ToHashSet();
        var sameTopicCounts = q.Topic != NoTopic;
        return all.Where(o => o.Id != q.Id)
            .Select(o => (o, score: (sameTopicCounts && string.Equals(o.Topic, q.Topic, StringComparison.OrdinalIgnoreCase) ? 10 : 0)
                                    + Words(o.Question + " " + o.Topic).Count(mine.Contains)))
            .Where(x => x.score >= 2)
            .OrderByDescending(x => x.score).ThenByDescending(x => x.o.PublishedAt)
            .Take(max).Select(x => x.o).ToList();
    }

    private static int Score(PublicQuestion q, List<string> words)
    {
        if (words.Count == 0) return 1;
        var score = 0;
        foreach (var w in words)
        {
            var inQ = q.Question.Contains(w, StringComparison.OrdinalIgnoreCase);
            var inRest = q.Answer.Contains(w, StringComparison.OrdinalIgnoreCase) || q.Topic.Contains(w, StringComparison.OrdinalIgnoreCase);
            if (!inQ && !inRest) return 0;
            score += inQ ? 2 : 1;
        }
        return score;
    }

    // Words worth matching on: 3+ letters and not the small Danish words every question has.
    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "og", "det", "der", "den", "jeg", "du", "kan", "man", "har", "hvad", "hvordan", "hvor", "hvis", "som", "med", "til", "for",
        "på", "en", "et", "er", "at", "af", "min", "mit", "mine", "din", "dit", "dine", "ikke", "også", "når", "skal", "vil", "være",
        "hej", "tak", "noget", "nogen", "fra", "eller", "men", "så", "lige", "bare", "mig", "dig", "the", "and",
    };

    internal static IEnumerable<string> Words(string text) =>
        NonWord().Split(text.ToLowerInvariant())
                 .Where(w => w.Length >= 3 && !Stop.Contains(w))
                 .Distinct();
}
