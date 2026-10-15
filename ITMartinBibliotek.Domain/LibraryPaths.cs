namespace ITMartinBibliotek.Domain;

// What a file's place on disk says about it: the category a film sits in, and the
// Jellyfin library (and so the owner) it belongs to.
public static class LibraryPaths
{
    // Film\X-Men\X2 (2003)\X2 (2003).mkv -> "X-Men". The title's own folder is skipped
    // (a movie Path is the file, a series Path is its folder); the next folder up is the
    // collection unless it is a library root, i.e. the title is not grouped.
    public static string CollectionFromPath(string? path, string type, IReadOnlySet<string> roots)
    {
        if (string.IsNullOrEmpty(path)) return "";
        var parts = path.Replace('\\', '/').TrimEnd('/').Split('/');
        var up = type == "Series" ? 1 : 2;
        if (parts.Length <= up) return "";
        var parent = string.Join('/', parts[..^up]);
        return roots.Contains(parent) ? "" : parts[^(up + 1)];
    }

    public static string Norm(string path) => path.Replace('\\', '/').TrimEnd('/');

    // The library whose folder holds the path. The longest location wins, so a customer
    // folder nested inside a household library (Musik/Kunder/Jytte) still counts as the customer's.
    public static string LibraryForPath(string? path, IEnumerable<(string Name, List<string> Locations)> libraries)
    {
        if (string.IsNullOrEmpty(path)) return "";
        var p = Norm(path) + "/";
        return libraries
            .SelectMany(l => l.Locations.Select(loc => (l.Name, Loc: Norm(loc) + "/")))
            .Where(x => p.StartsWith(x.Loc, StringComparison.Ordinal))
            .OrderByDescending(x => x.Loc.Length)
            .Select(x => x.Name)
            .FirstOrDefault() ?? "";
    }

    public static MediaFormat? GuessFormat(string text)
    {
        if (text.Contains("blu", StringComparison.OrdinalIgnoreCase) || text.Contains("4k", StringComparison.OrdinalIgnoreCase)) return MediaFormat.BluRay;
        if (text.Contains("dvd", StringComparison.OrdinalIgnoreCase)) return MediaFormat.Dvd;
        if (text.Contains("cd", StringComparison.OrdinalIgnoreCase)) return MediaFormat.Cd;
        return null;
    }
}
