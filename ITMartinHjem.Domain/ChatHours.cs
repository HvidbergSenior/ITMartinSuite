namespace ITMartinHjem.Domain;

/// <summary>When Martin can chat - fixed weekly hours (user 2026-11-13: "they need to see exactly when I'm able to chat... show
/// automatically", chose fixed weekly hours). Written once as text, fx "fre 12-13; man 10-12"; the site works out by itself
/// whether he is free now and when the next time is.</summary>
public sealed class ChatHours
{
    public sealed record Slot(DayOfWeek Day, TimeOnly From, TimeOnly To);

    private static readonly string[] Days = ["søn", "man", "tir", "ons", "tor", "fre", "lør"];
    private static readonly string[] Long = ["søndag", "mandag", "tirsdag", "onsdag", "torsdag", "fredag", "lørdag"];

    public IReadOnlyList<Slot> Slots { get; }

    private ChatHours(IReadOnlyList<Slot> slots) => Slots = slots;

    /// <summary>"fre 12-13; man 10:30-12" - bad parts are skipped, never throw (a typo must not take the site down).</summary>
    public static ChatHours Parse(string? text)
    {
        var slots = new List<Slot>();
        foreach (var part in (text ?? "").Split(';', ',', '\n'))
        {
            var bits = part.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (bits.Length != 2) continue;
            var d = Array.FindIndex(Days, x => bits[0].StartsWith(x));
            var span = bits[1].Split('-');
            if (d < 0 || span.Length != 2 || !TryTime(span[0], out var from) || !TryTime(span[1], out var to) || to <= from) continue;
            slots.Add(new Slot((DayOfWeek)d, from, to));
        }
        return new ChatHours(slots);
    }

    private static bool TryTime(string s, out TimeOnly t)
    {
        s = s.Replace('.', ':');
        if (int.TryParse(s, out var h) && h is >= 0 and <= 24) { t = h == 24 ? new TimeOnly(23, 59) : new TimeOnly(h, 0); return true; }
        return TimeOnly.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out t);
    }

    /// <summary>Open now? and the Danish line to show: "Jeg kan snakke nu – til kl. 13" or "Næste gang: i dag kl. 12–13".</summary>
    public (bool Open, string Text) Describe(DateTime nowLocal)
    {
        if (Slots.Count == 0) return (false, "");
        var now = TimeOnly.FromDateTime(nowLocal);
        foreach (var s in Slots)
            if (s.Day == nowLocal.DayOfWeek && now >= s.From && now < s.To) return (true, $"Jeg kan snakke nu – til kl. {T(s.To)}");

        for (var i = 0; i < 8; i++)
        {
            var day = nowLocal.Date.AddDays(i);
            var next = Slots.Where(s => s.Day == day.DayOfWeek && (i > 0 || s.From > now)).OrderBy(s => s.From).FirstOrDefault();
            if (next is null) continue;
            var when = i == 0 ? "i dag" : i == 1 ? "i morgen" : Long[(int)day.DayOfWeek];
            return (false, $"Næste gang jeg kan snakke: {when} kl. {T(next.From)}–{T(next.To)}");
        }
        return (false, "");
    }

    /// <summary>All the hours as one line: "fredag kl. 12–13 · mandag kl. 10–12".</summary>
    public string Week() => string.Join(" · ", Slots.OrderBy(s => ((int)s.Day + 6) % 7).ThenBy(s => s.From)
        .Select(s => $"{Long[(int)s.Day]} kl. {T(s.From)}–{T(s.To)}"));

    private static string T(TimeOnly t) => t.Minute == 0 ? t.Hour.ToString() : t.ToString("H:mm");
}
