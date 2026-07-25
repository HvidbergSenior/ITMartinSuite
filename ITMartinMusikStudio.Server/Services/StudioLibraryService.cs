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
    //
    // Recordings come out of the browser's MediaRecorder as .webm, which
    // doesn't play reliably in Safari/iOS - exactly where most family
    // listeners on the public app actually are. Transcode to mp3 (audio) or
    // mp4 (video) at publish time instead of copying the raw file, same
    // "convert to a universally playable format" convention the file-sorter
    // apps already use.
    //
    // Filename is "{songKey}__{timestamp}" - each publish ADDS a version
    // instead of overwriting the last one. The public listener app (which
    // owns no data of its own here, only scans this folder) groups files by
    // the part before "__" and lets people hide/delete individual versions
    // without touching this app.
    public async Task<bool> PublishRecordingAsync(string songKey, string relativePath)
    {
        if (!Exists(relativePath)) return false;
        var src = Path.GetFullPath(Path.Combine(Root, relativePath));
        Directory.CreateDirectory(MyVersionsDir);

        var isVideo = IsVideo(relativePath);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var dest = Path.Combine(MyVersionsDir, $"{songKey}__{stamp}.{(isVideo ? "mp4" : "mp3")}");

        var psi = new System.Diagnostics.ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(src);
        var extraArgs = isVideo
            ? new[] { "-c:v", "libx264", "-preset", "veryfast", "-crf", "23", "-c:a", "aac", "-movflags", "+faststart" }
            : new[] { "-vn", "-c:a", "libmp3lame", "-q:a", "2" };
        foreach (var arg in extraArgs) psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add(dest);

        bool converted;
        try
        {
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) { converted = false; }
            else
            {
                await proc.WaitForExitAsync();
                converted = proc.ExitCode == 0 && File.Exists(dest);
            }
        }
        catch
        {
            converted = false;
        }

        if (!converted)
        {
            // ffmpeg missing or failed - fall back to a plain copy so the
            // publish still succeeds rather than silently doing nothing.
            var fallbackDest = Path.Combine(MyVersionsDir, $"{songKey}__{stamp}{Path.GetExtension(src)}");
            File.Copy(src, fallbackDest, overwrite: true);
            return true;
        }

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

    // Attaches/replaces a song's SourceFile (the reference track used for
    // play-along overdub and chord-from-audio detection) from the Studio
    // page directly - previously this could only ever be set once, at song
    // creation, from a query-string param supplied by whatever flow created
    // the song, with no way to attach or swap it afterward. This is the
    // right home for a manually-downloaded Suno track: it's the song's
    // actual source material, not just one comparison take among others.
    public async Task<string> SaveSourceFileAsync(string songKey, string originalFileName, Stream content)
    {
        var dir = Path.Combine(Root, "uploads");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(originalFileName) is { Length: > 0 } e ? e : ".mp3";
        var dest = Path.Combine(dir, $"{songKey}-{DateTime.UtcNow:yyyyMMdd-HHmmss}{ext}");
        await using (var fs = File.Create(dest))
            await content.CopyToAsync(fs);
        return Path.GetRelativePath(Root, dest).Replace('\\', '/');
    }

    public string SketchesDir => Path.Combine(Root, "sketches");

    // Short "hum an idea" clips for the Skriv sang (from-scratch) flow -
    // deliberately a separate storage location and naming convention from
    // GetRecordings()'s take-/vtake-/aitake- files, since a sketch isn't a
    // take and must never show up in the Optagelser list.
    public List<SketchFile> GetSketches(string songKey)
    {
        var dir = Path.Combine(SketchesDir, songKey);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir)
            .Select(f => new SketchFile(
                Path.GetRelativePath(Root, f).Replace('\\', '/'),
                Path.GetFileNameWithoutExtension(f),
                new FileInfo(f).CreationTimeUtc))
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    public async Task SaveSketchAsync(string songKey, Stream content, string contentType)
    {
        var dir = Path.Combine(SketchesDir, songKey);
        Directory.CreateDirectory(dir);
        var ext = contentType.Contains("webm") ? ".webm" : ".ogg";
        var dest = Path.Combine(dir, $"sketch-{DateTime.UtcNow:yyyyMMdd-HHmmss}{ext}");
        await using var fs = File.Create(dest);
        await content.CopyToAsync(fs);
    }

    // Backs the "version" checklist step - true once at least one take has
    // been published via PublishRecordingAsync for this song.
    public bool HasPublishedVersion(string songKey) =>
        Directory.Exists(MyVersionsDir) &&
        Directory.EnumerateFiles(MyVersionsDir, $"{songKey}__*").Any();

    private static string Transliterate(string s) => s
        .Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa")
        .Replace("Æ", "Ae").Replace("Ø", "Oe").Replace("Å", "Aa");

    public string SafeKey(string sourceFile) =>
        Transliterate(Path.GetFileNameWithoutExtension(sourceFile))
            .ToLower()
            .Replace(" ", "_")
            .Replace("-", "_");
}

public record SourceFile(string RelativePath, string Title);
public record RecordingFile(string RelativePath, string Name, DateTime CreatedAt, bool IsVideo, bool IsAi = false);
public record SketchFile(string RelativePath, string Name, DateTime CreatedAt);
