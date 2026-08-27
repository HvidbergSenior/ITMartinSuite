using System.Text.Json;

namespace ITMartinMusikStudio.Server.Services;

// Sidecar metadata for SongVault - deliberately not the StudioSong EF entity
// (no DB row, no migration) since SongVault songs are keyed purely by
// filesystem SafeKey, same as stems/recordings already are. One JSON file
// per song, plain and inspectable, matching the "easier to understand"
// simplification SongVault is for.
public sealed record SongMeta(string Lyrics = "", string Chords = "", double? Tempo = null);

public sealed class StudioLibraryService
{
    private static readonly string[] VideoExt = [".mp4", ".mov", ".avi", ".webm", ".mkv", ".m4v"];
    private static readonly string[] AudioExt = [".mp3", ".m4a", ".wav", ".ogg", ".flac", ".aac"];

    public string Root { get; }
    public string RecordingsDir => Path.Combine(Root, "recordings");
    public string MyVersionsDir => Path.Combine(Root, "myversions");
    public string StemsDir => Path.Combine(Root, "stems");
    public string MetaDir => Path.Combine(Root, "meta");

    public StudioLibraryService(IConfiguration config)
    {
        Root = config["MusicSettings:Root"] ?? "/musik";
    }

    public SongMeta LoadMeta(string songKey)
    {
        var path = Path.Combine(MetaDir, $"{songKey}.json");
        if (!File.Exists(path)) return new SongMeta();
        try { return JsonSerializer.Deserialize<SongMeta>(File.ReadAllText(path)) ?? new SongMeta(); }
        catch { return new SongMeta(); }
    }

    public void SaveMeta(string songKey, SongMeta meta)
    {
        Directory.CreateDirectory(MetaDir);
        File.WriteAllText(Path.Combine(MetaDir, $"{songKey}.json"), JsonSerializer.Serialize(meta));
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
                if (name is "recordings" or "myversions" or "lyrics" or "originals" or "stems" or "meta") continue;
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

    // Reads back whatever StemService.SeparateAsync already wrote to disk for
    // this song, if any - ExtractStems()'s _stems field is otherwise pure
    // in-memory Blazor circuit state, forgotten on every page reload even
    // though the actual WAV files are sitting right there. Null (not an
    // empty StemResult) when nothing's been extracted yet, so callers can
    // tell "never run" apart from "ran, found nothing".
    public StemResult? GetExistingStems(string songKey)
    {
        var dir = Path.Combine(StemsDir, songKey);
        if (!Directory.Exists(dir)) return null;

        var result = new StemResult(
            ExistsOrNull(dir, "vocals.wav"),
            ExistsOrNull(dir, "drums.wav"),
            ExistsOrNull(dir, "bass.wav"),
            ExistsOrNull(dir, "other.wav"),
            ExistsOrNull(dir, "instrumental.wav"));

        return result is { Vocals: null, Drums: null, Bass: null, Other: null, Instrumental: null } ? null : result;
    }

    private static string? ExistsOrNull(string dir, string file)
    {
        var path = Path.Combine(dir, file);
        return File.Exists(path) ? path : null;
    }

    // Combines a mic-only take with a backing track (typically the "other"
    // demucs stem, i.e. the instrumental) into one mixed-down file - overdub
    // recording only ever captures the mic (see studio.js's startOverdub:
    // the backing track plays via a plain Audio() element, never mixed into
    // the MediaRecorder stream), so without this there's no way to get a
    // single file with both. "mixtake-" follows the same take-/vtake-/aitake-
    // filename-prefix convention GetRecordings() already parses.
    public async Task<bool> MixdownAsync(string songKey, string vocalRelativePath, string instrumentalFullPath)
    {
        var vocalSrc = Path.GetFullPath(Path.Combine(Root, vocalRelativePath));
        if (!File.Exists(vocalSrc) || !File.Exists(instrumentalFullPath)) return false;

        var dir = Path.Combine(RecordingsDir, songKey);
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, $"mixtake-{DateTime.UtcNow:yyyyMMdd-HHmmss}.mp3");

        var psi = new System.Diagnostics.ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(vocalSrc);
        psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(instrumentalFullPath);
        // duration=longest (not "shortest"/default): a short take shouldn't
        // truncate the backing track, and vice versa - whichever is longer
        // just plays out with the other silent for the remainder.
        psi.ArgumentList.Add("-filter_complex");
        psi.ArgumentList.Add("[0:a][1:a]amix=inputs=2:duration=longest:dropout_transition=0[a]");
        psi.ArgumentList.Add("-map"); psi.ArgumentList.Add("[a]");
        psi.ArgumentList.Add("-c:a"); psi.ArgumentList.Add("libmp3lame");
        psi.ArgumentList.Add("-q:a"); psi.ArgumentList.Add("2");
        psi.ArgumentList.Add(dest);

        try
        {
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0 && File.Exists(dest);
        }
        catch
        {
            return false;
        }
    }

    // Concatenates one chosen take per song section (already in song order)
    // into a single final take - the "record a section at a time, then comp
    // together the best takes" workflow. Uses ffmpeg's concat *filter* (not
    // the concat demuxer) since it decodes each input first, so it doesn't
    // care that the takes may have slightly different codecs/sample rates
    // (they're all browser MediaRecorder webm, but from different recording
    // sessions) - same "filter_complex, not stream copy" approach MixdownAsync
    // already uses above.
    public async Task<bool> MergeSectionTakesAsync(string songKey, List<string> orderedRelativePaths)
    {
        if (orderedRelativePaths.Count == 0) return false;
        var fullPaths = orderedRelativePaths
            .Select(p => Path.GetFullPath(Path.Combine(Root, p)))
            .ToList();
        if (fullPaths.Any(p => !File.Exists(p))) return false;

        var dir = Path.Combine(RecordingsDir, songKey);
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, $"mergetake-{DateTime.UtcNow:yyyyMMdd-HHmmss}.mp3");

        var psi = new System.Diagnostics.ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-y");
        foreach (var p in fullPaths) { psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(p); }

        var inputTags = string.Concat(Enumerable.Range(0, fullPaths.Count).Select(i => $"[{i}:a]"));
        psi.ArgumentList.Add("-filter_complex");
        psi.ArgumentList.Add($"{inputTags}concat=n={fullPaths.Count}:v=0:a=1[a]");
        psi.ArgumentList.Add("-map"); psi.ArgumentList.Add("[a]");
        psi.ArgumentList.Add("-c:a"); psi.ArgumentList.Add("libmp3lame");
        psi.ArgumentList.Add("-q:a"); psi.ArgumentList.Add("2");
        psi.ArgumentList.Add(dest);

        try
        {
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0 && File.Exists(dest);
        }
        catch
        {
            return false;
        }
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

    // Matches take-{section?}-{timestamp} filenames - the optional section
    // slug (letters/digits only, see the "record one verse at a time"
    // section picker) sits between the take-type prefix and the fixed
    // yyyyMMdd-HHmmss timestamp.
    private static readonly System.Text.RegularExpressions.Regex TakeFilenamePattern = new(
        @"^(?:take|vtake|aitake|mixtake|mergetake)(?:-(?<section>[a-z0-9]+))?-\d{8}-\d{6}$",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public List<RecordingFile> GetRecordings(string songKey)
    {
        var dir = Path.Combine(RecordingsDir, songKey);
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir)
            .Where(f => AudioExt.Concat(new[] { ".webm", ".ogg" })
                                .Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var m = TakeFilenamePattern.Match(name);
                var section = m.Success && m.Groups["section"].Success ? m.Groups["section"].Value : null;
                return new RecordingFile(
                    Path.GetRelativePath(Root, f).Replace('\\', '/'),
                    name,
                    new FileInfo(f).CreationTimeUtc,
                    name.StartsWith("vtake"),
                    name.StartsWith("aitake"),
                    section);
            })
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
public record RecordingFile(string RelativePath, string Name, DateTime CreatedAt, bool IsVideo, bool IsAi = false, string? Section = null);
public record SketchFile(string RelativePath, string Name, DateTime CreatedAt);
