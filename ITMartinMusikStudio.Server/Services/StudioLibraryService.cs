namespace ITMartinMusikStudio.Server.Services;

public sealed class StudioLibraryService
{
    private static readonly string[] VideoExt = [".mp4", ".mov", ".avi", ".webm", ".mkv", ".m4v"];
    private static readonly string[] AudioExt = [".mp3", ".m4a", ".wav", ".ogg", ".flac", ".aac"];

    public string Root { get; }
    public string RecordingsDir => Path.Combine(Root, "recordings");
    public string MyVersionsDir => Path.Combine(Root, "myversions");

    public StudioLibraryService(IConfiguration config)
    {
        Root = config["MusicSettings:Root"] ?? "/musik";
    }

    public List<SourceFile> GetSourceFiles()
    {
        var all = VideoExt.Concat(AudioExt).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<SourceFile>();

        if (!Directory.Exists(Root)) return results;

        try
        {
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.TopDirectoryOnly))
            {
                var ext = Path.GetExtension(file);
                if (!all.Contains(ext)) continue;
                var rel = Path.GetRelativePath(Root, file).Replace('\\', '/');
                results.Add(new SourceFile(rel, Path.GetFileNameWithoutExtension(file)));
            }

            foreach (var dir in Directory.EnumerateDirectories(Root))
            {
                var name = Path.GetFileName(dir);
                if (name is "recordings" or "myversions" or "lyrics" or "originals") continue;
                if (name.StartsWith('.') || name.StartsWith('@')) continue;

                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file);
                    if (!all.Contains(ext)) continue;
                    var rel = Path.GetRelativePath(Root, file).Replace('\\', '/');
                    results.Add(new SourceFile(rel, Path.GetFileNameWithoutExtension(file)));
                }
            }
        }
        catch { }

        results.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    public bool IsVideo(string relativePath) =>
        VideoExt.Contains(Path.GetExtension(relativePath), StringComparer.OrdinalIgnoreCase);

    public bool Exists(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        return full.StartsWith(Root, StringComparison.OrdinalIgnoreCase) && File.Exists(full);
    }

    public List<RecordingFile> GetRecordings(string songKey)
    {
        var dir = Path.Combine(RecordingsDir, songKey);
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir)
            .Where(f => AudioExt.Concat(new[] { ".webm", ".ogg" })
                                .Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => new RecordingFile(
                Path.GetRelativePath(Root, f).Replace('\\', '/'),
                Path.GetFileNameWithoutExtension(f),
                new FileInfo(f).CreationTimeUtc))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public string SafeKey(string sourceFile) =>
        Path.GetFileNameWithoutExtension(sourceFile)
            .ToLower()
            .Replace(" ", "_")
            .Replace("-", "_");
}

public record SourceFile(string RelativePath, string Title);
public record RecordingFile(string RelativePath, string Name, DateTime CreatedAt);
