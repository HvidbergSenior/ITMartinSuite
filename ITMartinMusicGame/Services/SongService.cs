using ITMartinMusicGame.Models;

namespace ITMartinMusicGame.Services;

public class SongService
{
    private static readonly string[] AudioExt = [".mp3", ".m4a", ".wav", ".ogg", ".flac", ".aac"];
    private static readonly string[] VideoExt = [".mp4", ".mov", ".webm", ".mkv", ".m4v"];

    public string Root { get; }

    public SongService(IConfiguration config)
    {
        Root = config["MusicSettings:Root"] ?? "/musik";
    }

    public List<GameSong> GetAll()
    {
        var all = AudioExt.Concat(VideoExt).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<GameSong>();
        if (!Directory.Exists(Root)) return results;

        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
            {
                if (!all.Contains(Path.GetExtension(file))) continue;
                var rel = Path.GetRelativePath(Root, file).Replace('\\', '/');
                // Skip recordings/myversions subfolders
                var parts = rel.Split('/');
                if (parts.Any(p => p is "recordings" or "myversions" or "originals")) continue;
                results.Add(new GameSong(rel, Path.GetFileNameWithoutExtension(file)));
            }
        }
        catch { }

        return results;
    }

    public List<GameSong> PickRandom(int count, IEnumerable<string> exclude)
    {
        var all = GetAll().Where(s => !exclude.Contains(s.RelativePath)).ToList();
        if (all.Count == 0) return [];
        var picked = new List<GameSong>();
        while (picked.Count < count && all.Count > 0)
        {
            var i = Random.Shared.Next(all.Count);
            picked.Add(all[i]);
            all.RemoveAt(i);
        }
        return picked;
    }

    public string? GetLyrics(string relativePath)
    {
        // Look for a .txt file with the same base name, in the same folder
        var dir = Path.GetDirectoryName(Path.Combine(Root, relativePath)) ?? Root;
        var name = Path.GetFileNameWithoutExtension(relativePath);
        var txt = Path.Combine(dir, name + ".txt");
        if (File.Exists(txt)) return File.ReadAllText(txt).Trim();

        // Try a lyrics subfolder
        var lyricsSub = Path.Combine(dir, "lyrics", name + ".txt");
        if (File.Exists(lyricsSub)) return File.ReadAllText(lyricsSub).Trim();

        return null;
    }

    public string GetStreamUrl(string relativePath) => $"/song/{Uri.EscapeDataString(relativePath)}";

    public bool Exists(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        return full.StartsWith(Root, StringComparison.OrdinalIgnoreCase) && File.Exists(full);
    }
}
