namespace ITMartinHjem.Domain;

// "Kritik" on itmartin.dk (user 2026-10-29: "a Kritik session - for Ros/ris and suggestions... I can write whatever I want about
// myself. What matters are how other people look at me. So here you can comment on my way of working").
// A visitor writes ros, ris or a forslag; nothing is shown before Martin has read it and pressed Vis - and only if the visitor
// said it may be shown. A name is shown only if the visitor gave one; phone numbers and e-mails are taken out on the way in.
public sealed class Feedback
{
    public const int MaxText = 2000, MaxName = 40, MaxReply = 2000;

    public int Id { get; private set; }
    public string Kind { get; private set; } = FeedbackRules.Praise;
    public string Text { get; private set; } = "";
    public string Name { get; private set; } = "";
    public bool MayShow { get; private set; }
    public bool Shown { get; private set; }
    public string Reply { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }

    private Feedback() { }   // EF

    public static Feedback Write(string? kind, string? text, string? name, bool mayShow, DateTime now)
    {
        var t = FeedbackRules.Clean(text, MaxText);
        if (t.Length == 0) throw new ArgumentException("Skriv, hvad du synes.");
        return new Feedback
        {
            Kind = FeedbackRules.NormalizeKind(kind), Text = t, Name = FeedbackRules.Clean(name, MaxName), MayShow = mayShow, CreatedAt = now,
        };
    }

    /// <summary>Martin shows it on the page (only possible when the visitor said yes) - with an optional reply.</summary>
    public void Show(string? reply)
    {
        if (!MayShow) throw new InvalidOperationException("Den, der skrev, har ikke sagt ja til, at det må vises.");
        Shown = true;
        Reply = FeedbackRules.Clean(reply, MaxReply);
    }

    public void Hide() => Shown = false;

    /// <summary>Martin's reply without showing it (fx to a ris that stays private).</summary>
    public void Answer(string? reply) => Reply = FeedbackRules.Clean(reply, MaxReply);
}

public static class FeedbackRules
{
    public const string Praise = "ros", Criticism = "ris", Suggestion = "forslag";
    public static readonly IReadOnlyList<(string Kind, string Title)> Kinds = [(Praise, "Ros"), (Criticism, "Ris"), (Suggestion, "Forslag")];

    public static string NormalizeKind(string? kind) =>
        Kinds.Any(k => k.Kind == (kind ?? "").Trim().ToLowerInvariant()) ? kind!.Trim().ToLowerInvariant() : Praise;

    public static string Title(string kind) => Kinds.FirstOrDefault(k => k.Kind == kind).Title ?? "Ros";

    /// <summary>Trimmed, cut to max, and no phone numbers or e-mails (the same anonymising as the public questions).</summary>
    public static string Clean(string? text, int max)
    {
        var t = QuestionRules.Anonymize((text ?? "").Trim());
        return t.Length > max ? t[..max] : t;
    }

    /// <summary>What everyone sees: only shown ones, newest first.</summary>
    public static List<Feedback> Public(IEnumerable<Feedback> all) => all.Where(f => f.Shown && f.MayShow).OrderByDescending(f => f.CreatedAt).ToList();
}
