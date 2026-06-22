namespace ITMartinMusic.Server.Services;

public sealed class MusicLibraryService
{
    private readonly string _root;

    private static readonly string[] AudioExt = [".mp3", ".m4a", ".wav", ".ogg", ".flac", ".aac"];
    private static readonly string[] VideoExt = [".mp4", ".mov", ".avi", ".webm", ".mkv", ".m4v"];

    public MusicLibraryService(IConfiguration config)
    {
        _root = config["MusicSettings:Root"] ?? "/musik";
    }

    public IReadOnlyList<string> GetAllFiles()
    {
        if (!Directory.Exists(_root))
            return [];

        var all = AudioExt.Concat(VideoExt).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<string>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.TopDirectoryOnly))
            {
                if (all.Contains(Path.GetExtension(file)))
                    results.Add(Path.GetRelativePath(_root, file).Replace('\\', '/'));
            }

            foreach (var dir in Directory.EnumerateDirectories(_root))
            {
                var name = Path.GetFileName(dir);
                if (name.StartsWith('@') || name.StartsWith('#') || name.StartsWith('.'))
                    continue;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        if (all.Contains(Path.GetExtension(file)))
                            results.Add(Path.GetRelativePath(_root, file).Replace('\\', '/'));
                    }
                }
                catch { /* skip inaccessible subdirectories */ }
            }
        }
        catch { /* skip if root is inaccessible */ }

        results.Sort();
        return results;
    }

    public bool IsVideo(string relativePath) =>
        VideoExt.Contains(Path.GetExtension(relativePath), StringComparer.OrdinalIgnoreCase);

    public bool Exists(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        return full.StartsWith(_root, StringComparison.OrdinalIgnoreCase) && File.Exists(full);
    }

    public string Root => _root;
}
