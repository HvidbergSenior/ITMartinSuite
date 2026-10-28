using System.Collections.Concurrent;
using ITMartinGallery.Domain;

namespace ITMartinGallery.Application;

/// <summary>
/// "Lidt navne rundtomkring" (theme "bedstemor"): which family members are in a picture, read from the person folders
/// FileSorter made (SmartFolders/People/&lt;name&gt;). A copy there has the same file name and size as the original,
/// so (name, size) finds it again - the size check keeps two cameras' "P1240049.jpg" apart.
/// Person folders can contain the odd wrong face, so the names are a friendly hint, not a fact.
/// </summary>
public sealed class PeopleIndex(ILibraryDisk disk)
{
    private readonly ConcurrentDictionary<string, (DateTime Expires, Dictionary<(string, long), string[]> Map, string[] Names)> _cache = new();

    public (Dictionary<(string, long), string[]> Map, string[] Names) Get(string galleryRoot)
    {
        if (_cache.TryGetValue(galleryRoot, out var hit) && hit.Expires > DateTime.UtcNow) return (hit.Map, hit.Names);

        var map = new Dictionary<(string, long), List<string>>();
        var names = new List<string>();
        var peopleDir = Path.Combine(galleryRoot, PeopleNames.PeopleRel);
        if (disk.DirectoryExists(peopleDir))
        {
            foreach (var dir in disk.Directories(peopleDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var person = PeopleNames.DisplayName(Path.GetFileName(dir));
                names.Add(Path.GetFileName(dir));
                foreach (var f in disk.AllFiles(dir))
                {
                    if (f.Contains("_Galleri", StringComparison.OrdinalIgnoreCase)) continue;
                    long len;
                    try { len = disk.Length(f); } catch { continue; }
                    var key = (Path.GetFileName(f).ToLowerInvariant(), len);
                    if (!map.TryGetValue(key, out var list)) map[key] = list = [];
                    if (!list.Contains(person)) list.Add(person);
                }
            }
        }
        var result = map.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        _cache[galleryRoot] = (DateTime.UtcNow.AddHours(1), result, names.ToArray());
        return (result, names.ToArray());
    }

    public string[]? For(Dictionary<(string, long), string[]>? map, string file)
    {
        if (map is null) return null;
        try { return map.GetValueOrDefault((Path.GetFileName(file).ToLowerInvariant(), disk.Length(file))); }
        catch { return null; }
    }
}
