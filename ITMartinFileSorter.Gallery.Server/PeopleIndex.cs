using System.Collections.Concurrent;

/// <summary>
/// "Lidt navne rundtomkring" (theme "bedstemor"): which family members are in a picture, read from the person folders
/// FileSorter made (SmartFolders/People/&lt;name&gt;). A copy there has the same file name and size as the original,
/// so (name, size) finds it again - the size check keeps two cameras' "P1240049.jpg" apart.
/// Person folders can contain the odd wrong face, so the names are a friendly hint, not a fact.
/// </summary>
static class PeopleIndex
{
    private const string PeopleRel = "SmartFolders/People";
    private static readonly ConcurrentDictionary<string, (DateTime Expires, Dictionary<(string, long), string[]> Map, string[] Names)> Cache = new();

    // Folder names are ASCII-safe; show them the way the family writes them.
    private static readonly Dictionary<string, string> Display = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Joergen"] = "Jørgen",
    };

    public static string DisplayName(string folder) => Display.GetValueOrDefault(folder, folder);

    public static (Dictionary<(string, long), string[]> Map, string[] Names) Get(string galleryRoot)
    {
        if (Cache.TryGetValue(galleryRoot, out var hit) && hit.Expires > DateTime.UtcNow) return (hit.Map, hit.Names);

        var map = new Dictionary<(string, long), List<string>>();
        var names = new List<string>();
        var peopleDir = Path.Combine(galleryRoot, PeopleRel);
        if (Directory.Exists(peopleDir))
        {
            foreach (var dir in Directory.GetDirectories(peopleDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var person = DisplayName(Path.GetFileName(dir));
                names.Add(Path.GetFileName(dir));
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if (f.Contains("_Galleri", StringComparison.OrdinalIgnoreCase)) continue;
                    long len;
                    try { len = new FileInfo(f).Length; } catch { continue; }
                    var key = (Path.GetFileName(f).ToLowerInvariant(), len);
                    if (!map.TryGetValue(key, out var list)) map[key] = list = [];
                    if (!list.Contains(person)) list.Add(person);
                }
            }
        }
        var result = map.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        Cache[galleryRoot] = (DateTime.UtcNow.AddHours(1), result, names.ToArray());
        return (result, names.ToArray());
    }

    public static string[]? For(Dictionary<(string, long), string[]>? map, string file)
    {
        if (map is null) return null;
        try { return map.GetValueOrDefault((Path.GetFileName(file).ToLowerInvariant(), new FileInfo(file).Length)); }
        catch { return null; }
    }
}
