namespace ITMartinHjem.Domain;

/// <summary>
/// itmartin.dk/&lt;app&gt; -> the app (user 2026-11-27: "people generally find it difficult to write ssss.itmartin.dk").
/// People remember itmartin.dk; the rest is one plain word: itmartin.dk/moede, /snak, /elpriser, /husk …
/// Words come from the apps' own addresses (snak.itmartin.dk -> snak) and names (Forløbet -> forloebet), plus a few fixed ones.
/// A real page on itmartin.dk with the same address always wins (checked by the caller).
/// </summary>
public static class ShortLinks
{
    /// <summary>Fixed words: apps that are not on the "Mine apps" list, or that should open one place inside the app.</summary>
    public static readonly IReadOnlyDictionary<string, string> Fixed = new Dictionary<string, string>
    {
        // Møde has one room today (følgegruppen) - the door asks for the code word from the mail.
        ["moede"] = "https://moede.itmartin.dk/gruppe1",
        ["gruppe1"] = "https://moede.itmartin.dk/gruppe1",
        // apps not on the "Mine apps" list
        ["snak"] = "https://snak.itmartin.dk",
        ["husk"] = "https://husk.itmartin.dk",
        // OPEN rooms and galleries only (user 2026-11-27): private ones (family, cases, password galleries) keep their long
        // secret link, sent as a button - a short word is easy to guess. Martin + Claude add words here; no self-service.
        ["aadalen"] = "https://snak.itmartin.dk/aadalen",   // "AI på Ådalen" - open course room (name only)
    };

    public static string? Target(string? word, IEnumerable<AppLink> apps)
    {
        var w = Normalize(word);
        if (w.Length is 0 or > 40 || w == "www") return null;
        if (Fixed.TryGetValue(w, out var url)) return url;
        foreach (var a in apps.Where(a => a.Url.StartsWith("http")))
            if (HostWord(a.Url) == w || Normalize(a.Name) == w) return a.Url;
        return null;
    }

    /// <summary>"Møde" -> "moede", "PC-tjek" -> "pc-tjek", "ElPriser" -> "elpriser" (Danish letters spelled out, like the addresses).</summary>
    public static string Normalize(string? s)
    {
        var t = (s ?? "").Trim().Trim('/').ToLowerInvariant()
            .Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa").Replace(' ', '-');
        return new string(t.Where(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-').ToArray());
    }

    private static string? HostWord(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return null;
        var host = u.Host.ToLowerInvariant();
        return host.EndsWith(".itmartin.dk") && host.Count(c => c == '.') == 2 ? host[..host.IndexOf('.')] : null;
    }
}
