using System.Globalization;
using System.Text;

namespace ITMartinSnak.Domain;

// 2026-11-09 "instead of opgaver here I want kalendertider ... and those dates should be on the frontpage" (Hold 36).
// The course keeps its own calendar (a public .ics feed, "Abonner på kalender" on its page). The room reads that feed,
// so a moved date shows in Snak too - nobody types the dates twice.
public sealed record CalendarEvent(DateTime StartUtc, DateTime EndUtc, string Title, string Place, bool AllDay);

public static class CalendarRules
{
    public const int MaxEvents = 200, MaxText = 200;
    private static readonly TimeZoneInfo Dk = TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen");

    /// <summary>webcal:// is the same address over https. Only http(s) addresses are kept; anything else = "".</summary>
    public static string CleanUrl(string? url)
    {
        var u = (url ?? "").Trim();
        if (u.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) u = "https://" + u[9..];
        return Uri.TryCreate(u, UriKind.Absolute, out var x) && (x.Scheme == "https" || x.Scheme == "http") ? x.AbsoluteUri : "";
    }

    /// <summary>The events of an iCalendar text, oldest first. Unknown lines are skipped; a broken event is left out.</summary>
    public static List<CalendarEvent> Parse(string? ics)
    {
        var list = new List<CalendarEvent>();
        Dictionary<string, (string Params, string Value)>? ev = null;
        foreach (var line in Unfold(ics ?? ""))
        {
            if (line == "BEGIN:VEVENT") { ev = new(); continue; }
            if (line == "END:VEVENT")
            {
                if (ev is not null && ToEvent(ev) is { } e) list.Add(e);
                ev = null;
                if (list.Count == MaxEvents) break;
                continue;
            }
            if (ev is null) continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var head = line[..colon];
            var semi = head.IndexOf(';');
            var name = (semi < 0 ? head : head[..semi]).ToUpperInvariant();
            ev.TryAdd(name, (semi < 0 ? "" : head[(semi + 1)..], line[(colon + 1)..]));
        }
        return list.OrderBy(e => e.StartUtc).ToList();
    }

    private static CalendarEvent? ToEvent(Dictionary<string, (string Params, string Value)> ev)
    {
        if (!ev.TryGetValue("DTSTART", out var s) || Time(s.Params, s.Value) is not { } start) return null;
        var end = ev.TryGetValue("DTEND", out var e) && Time(e.Params, e.Value) is { } en ? en.Utc : start.Utc;
        var title = Text(ev.GetValueOrDefault("SUMMARY").Value);
        if (title.Length == 0) title = "Kursusdag";
        return new CalendarEvent(start.Utc, end, title, Text(ev.GetValueOrDefault("LOCATION").Value), start.AllDay);
    }

    // "20261111T073000Z" (UTC), "TZID=Europe/Copenhagen:20261111T083000" (local), "VALUE=DATE:20261111" (all day).
    private static (DateTime Utc, bool AllDay)? Time(string parms, string value)
    {
        var v = value.Trim();
        if (v.Length == 8 && DateTime.TryParseExact(v, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            return (TimeZoneInfo.ConvertTimeToUtc(day, Dk), true);
        var utc = v.EndsWith('Z');
        if (!DateTime.TryParseExact(utc ? v[..^1] : v, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            return null;
        if (utc) return (DateTime.SpecifyKind(t, DateTimeKind.Utc), false);
        var zone = Dk;
        var tzid = parms.Split(';').FirstOrDefault(p => p.StartsWith("TZID=", StringComparison.OrdinalIgnoreCase));
        if (tzid is not null) { try { zone = TimeZoneInfo.FindSystemTimeZoneById(tzid[5..].Trim('"')); } catch { } }
        return (TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(t, DateTimeKind.Unspecified), zone), false);
    }

    // \n, \, and \; are escaped in iCalendar text; a place over several lines becomes one line with commas.
    private static string Text(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        var sb = new StringBuilder();
        for (var i = 0; i < v.Length; i++)
        {
            if (v[i] == '\\' && i + 1 < v.Length)
            {
                var c = v[++i];
                sb.Append(c is 'n' or 'N' ? ", " : c.ToString());
            }
            else sb.Append(v[i]);
        }
        var t = string.Join(", ", sb.ToString().Split(", ").Select(x => x.Trim()).Where(x => x.Length > 0)).Trim();
        return t.Length > MaxText ? t[..MaxText] : t;
    }

    // Long lines are folded: a line starting with a space or tab continues the one before.
    private static IEnumerable<string> Unfold(string ics)
    {
        string? cur = null;
        foreach (var raw in ics.Replace("\r\n", "\n").Split('\n'))
        {
            if (raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t')) { cur += raw[1..]; continue; }
            if (cur is not null) yield return cur;
            cur = raw.TrimEnd('\r');
        }
        if (cur is not null) yield return cur;
    }
}
