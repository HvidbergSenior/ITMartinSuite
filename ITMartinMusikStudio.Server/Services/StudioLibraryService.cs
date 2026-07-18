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
                if (name is "recordings" or "myversions" or "lyrics" or "originals" or "stems") continue;
                if (name.StartsWith('.') || name.StartsWith('@') || name.StartsWith('#')) continue;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        var ext = Path.GetExtension(file);
                        if (!all.Contains(ext)) continue;
                        var rel = Path.GetRelativePath(Root, file).Replace('\\', '/');
                        results.Add(new SourceFile(rel, Path.GetFileNameWithoutExtension(file)));
                    }
                }
                catch { }
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

    // Same reasoning as DeleteRecording below - direct call instead of a
    // loopback HTTP request to this app's own external URL.
    public bool PublishRecording(string songKey, string relativePath)
    {
        if (!Exists(relativePath)) return false;
        var src = Path.GetFullPath(Path.Combine(Root, relativePath));
        Directory.CreateDirectory(MyVersionsDir);
        var dest = Path.Combine(MyVersionsDir, $"{songKey}.webm");
        File.Copy(src, dest, overwrite: true);
        return true;
    }

    // Deletes a recording file directly rather than the caller making a
    // loopback HTTP call to this same app's own external URL for it - that
    // self-call broke when accessed through a reverse proxy (e.g. Tailscale
    // Serve) whose hostname isn't reachable from inside the container's own
    // network namespace. Same path-traversal guard as Exists().
    public bool DeleteRecording(string relativePath)
    {
        if (!Exists(relativePath)) return false;
        var full = Path.GetFullPath(Path.Combine(Root, relativePath));
        try
        {
            File.Delete(full);
            return true;
        }
        catch (IOException)
        {
            // Most commonly: the file is still open in an <audio> player on
            // the page (very plausible when someone's listening through
            // several takes before deleting one) - Windows locks it and
            // File.Delete throws. Previously this was unhandled and crashed
            // the whole Blazor circuit, which looked like "delete does
            // nothing" with no indication why.
            return false;
        }
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
                new FileInfo(f).CreationTimeUtc,
                Path.GetFileNameWithoutExtension(f).StartsWith("vtake"),
                Path.GetFileNameWithoutExtension(f).StartsWith("aitake")))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    // Manually downloaded from Suno (no official API to automate this, see
    // /songwriter) and uploaded here to sit alongside the sung takes for the
    // same song - same "aitake"/"vtake"/"take" filename-prefix convention
    // GetRecordings() already reads IsVideo/IsAi from, just a new prefix.
    public async Task SaveAiTakeAsync(string songKey, string originalFileName, Stream content)
    {
        var dir = Path.Combine(RecordingsDir, songKey);
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(originalFileName) is { Length: > 0 } e ? e : ".mp3";
        var dest = Path.Combine(dir, $"aitake-{DateTime.UtcNow:yyyyMMdd-HHmmss}{ext}");
        await using var fs = File.Create(dest);
        await content.CopyToAsync(fs);
    }

    public string SafeKey(string sourceFile) =>
        Path.GetFileNameWithoutExtension(sourceFile)
            .ToLower()
            .Replace(" ", "_")
            .Replace("-", "_");
}

public record SourceFile(string RelativePath, string Title);
public record RecordingFile(string RelativePath, string Name, DateTime CreatedAt, bool IsVideo, bool IsAi = false);
