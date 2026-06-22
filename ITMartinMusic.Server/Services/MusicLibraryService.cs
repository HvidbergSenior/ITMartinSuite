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

        return Directory
            .EnumerateFiles(_root, "*", SearchOption.AllDirectories)
            .Where(f => all.Contains(Path.GetExtension(f)))
            .Select(f => Path.GetRelativePath(_root, f).Replace('\\', '/'))
            .OrderBy(f => f)
            .ToList();
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
