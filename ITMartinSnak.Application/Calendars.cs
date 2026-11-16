using System.Collections.Concurrent;
using ITMartinSnak.Domain;

namespace ITMartinSnak.Application;

// Fetches a room's course calendar (.ics) and keeps it for a while, so 20 phones opening the room ask the course's
// calendar once, not 20 times. If the calendar cannot be reached, the last good copy is shown.
public interface ICalendarSource
{
    Task<string?> FetchAsync(string url, CancellationToken ct);
}

public sealed class Calendars(ICalendarSource source, TimeProvider clock)
{
    public static readonly TimeSpan KeepFor = TimeSpan.FromMinutes(20);
    private readonly ConcurrentDictionary<string, (DateTime At, List<CalendarEvent> Events)> _cache = new();

    public async Task<IReadOnlyList<CalendarEvent>> EventsAsync(string? url, CancellationToken ct = default)
    {
        var u = CalendarRules.CleanUrl(url);
        if (u.Length == 0) return [];
        var now = clock.GetUtcNow().UtcDateTime;
        if (_cache.TryGetValue(u, out var hit) && now - hit.At < KeepFor) return hit.Events;
        string? text = null;
        try { text = await source.FetchAsync(u, ct); } catch { }
        if (text is null) return hit.Events ?? [];
        var events = CalendarRules.Parse(text);
        _cache[u] = (now, events);
        return events;
    }

    public void Forget(string? url) => _cache.TryRemove(CalendarRules.CleanUrl(url), out _);
}
