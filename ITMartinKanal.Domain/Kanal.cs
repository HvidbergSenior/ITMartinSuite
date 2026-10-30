using System.Text.RegularExpressions;

namespace ITMartinKanal.Domain;

// Kanal (2026-10-30, "just for demo purposes"): an open comment channel on an old computer - first one for Pelle Dragsted
// (Enhedslisten), on data centres and green energy. It shows how little it takes: a small page on Martin's own photoserver,
// asleep at night. Anyone may write; a name is optional; phone numbers and e-mails are taken out on the way in.

public sealed class KanalComment
{
    public const int MaxText = 1500, MaxName = 40;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Channel { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime At { get; set; }

    public static KanalComment Write(KanalChannel channel, string? topic, string? name, string? text, DateTime now)
    {
        var t = KanalRules.Clean(text, MaxText);
        if (t.Length == 0) throw new ArgumentException("Skriv din kommentar først.");
        var top = channel.Topics.FirstOrDefault(x => x == (topic ?? "").Trim()) ?? channel.Topics.FirstOrDefault() ?? "";
        return new KanalComment { Channel = channel.Slug, Topic = top, Name = KanalRules.Clean(name, MaxName), Text = t, At = now };
    }
}

/// <summary>One channel: its address, a short intro, the topics people can choose and - optionally - the stories it tells
/// (2026-10-30: "how digital helpers helped a family with perspective").</summary>
public sealed record KanalChannel(string Slug, string Title, string Intro, string[] Topics, KanalStory[]? Stories = null);

/// <summary>One story box: a heading, "Hvorfor denne hjælper?" first (the thing that just was not right - like Forløbet,
/// user 2026-10-30), then the paragraphs.</summary>
public sealed record KanalStory(string Title, string[] Paragraphs, string Why = "");

public sealed class KanalData
{
    public List<KanalComment> Comments { get; set; } = [];
}

public static partial class KanalRules
{
    /// <summary>Trimmed, cut, and no phone numbers or e-mails (the same anonymising as itmartin.dk's questions).</summary>
    public static string Clean(string? text, int max)
    {
        var t = Email().Replace(text ?? "", "[skjult]");
        t = Phone().Replace(t, "[skjult]");
        t = string.Join("\n", t.Replace("\r", "").Split('\n').Select(l => l.Trim())).Trim();
        return t.Length > max ? t[..max] : t;
    }

    /// <summary>Newest first, only this channel's.</summary>
    public static List<KanalComment> For(IEnumerable<KanalComment> all, string channel) =>
        all.Where(c => c.Channel == channel).OrderByDescending(c => c.At).ToList();

    /// <summary>The page sleeps at night (23-07 Danish time): it shows "Siden sover" and takes no comments. Only the page
    /// sleeps - the computer it runs on never may (its USB drive does not survive a suspend).</summary>
    public static bool Asleep(DateTime danishNow) => danishNow.Hour >= 23 || danishNow.Hour < 7;

    private static readonly TimeZoneInfo Danish = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");
    public static DateTime DanishNow(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Danish);

    /// <summary>A guard against floods: at most this many comments a day per channel.</summary>
    public const int MaxPerDay = 200;

    public static bool Full(IEnumerable<KanalComment> all, string channel, DateTime now) =>
        all.Count(c => c.Channel == channel && c.At > now.AddDays(-1)) >= MaxPerDay;

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"(?<!\d)(?:\+?45[\s-]?)?(?:\d{2}[\s-]?){3}\d{2}(?!\d)")]
    private static partial Regex Phone();
}
