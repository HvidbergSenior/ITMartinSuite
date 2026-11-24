namespace ITMartinSnak.Domain;

// One phone (or browser) that gets beskeder from a room. Room "" = Martin's, every room. Host = the phone belongs to Martin
// (it was set up while he was logged in), also when it only follows one room (e.g. the Anton room on his home screen).
public sealed record PushPhone(string Endpoint, string Room, string Name, bool Host);

public static class PushRules
{
    // 2026-11-24 (user: "I get notification on Skejby Snak when writing on the Anton Snak"): what you write yourself never
    // gives a push on your own phones - not on the one you wrote from, not on your other phones (same name in the room),
    // and for Martin not on any of his phones (every-room or one room).
    /// <returns>The endpoints to send to, each once.</returns>
    public static IReadOnlyList<string> Targets(IEnumerable<PushPhone> phones, string room, string? fromEndpoint, string? fromName, bool fromHost)
    {
        var all = phones.ToList();
        var skip = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(fromEndpoint)) skip.Add(fromEndpoint);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(fromName)) names.Add(fromName.Trim());
        foreach (var p in all)
            if (p.Endpoint == fromEndpoint && p.Room == room && p.Name.Length > 0) names.Add(p.Name);
        // The sender's own phone is Martin's: then the message is Martin's too.
        fromHost |= all.Any(p => p.Endpoint == fromEndpoint && (p.Host || p.Room == ""));
        foreach (var p in all)
        {
            if (p.Room == room && names.Contains(p.Name)) skip.Add(p.Endpoint);
            if (fromHost && (p.Host || p.Room == "")) skip.Add(p.Endpoint);
        }
        return all.Where(p => (p.Room == room || p.Room == "") && !skip.Contains(p.Endpoint))
            .Select(p => p.Endpoint).Distinct(StringComparer.Ordinal).ToList();
    }
}
