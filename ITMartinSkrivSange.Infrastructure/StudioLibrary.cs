using System.Text.Json;
using System.Text.RegularExpressions;
using ITMartinSkrivSange.Application;

namespace ITMartinSkrivSange.Infrastructure;

// The music folder (MusicSettings:Root, /musik in the container). Every path from outside goes through Resolve, so nothing
// can point above the root. The studio's publish/mixdown/merge code was left in Musik Studio - SkrivSange never used it.
public sealed class StudioLibrary(string root) : IStudioLibrary
{
    private static readonly string[] VideoExt = [".mp4", ".mov", ".avi", ".webm", ".mkv", ".m4v"];
    private static readonly string[] AudioExt = [".mp3", ".m4a", ".wav", ".ogg", ".flac", ".aac"];

    private string RecordingsDir => Path.Combine(root, "recordings");
    private string MetaDir => Path.Combine(root, "meta");
    private string SketchesDir => Path.Combine(root, "sketches");

    public string StemsDir(string songKey) => Path.Combine(root, "stems", songKey);

    public string? Resolve(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var full = Path.GetFullPath(Path.Combine(root, relativePath));
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
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

        if (!Directory.Exists(root)) return results;

        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
            {
                if (!all.Contains(Path.GetExtension(file))) continue;
                results.Add(new SourceFile(Relative(file), Path.GetFileNameWithoutExtension(file)));
            }

            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var name = Path.GetFileName(dir);
                if (name is "recordings" or "myversions" or "lyrics" or "originals" or "stems" or "meta") continue;
                if (name.StartsWith('.') || name.StartsWith('@') || name.StartsWith('#')) continue;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        if (!all.Contains(Path.GetExtension(file))) continue;
                        results.Add(new SourceFile(Relative(file), Path.GetFileNameWithoutExtension(file)));
                    }
                }
                catch { }
            }
        }
        catch { }

        results.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    // Reads back whatever the stem separator already wrote to disk for this song, if any. Null (not an
    // empty StemResult) when nothing's been extracted yet, so callers can tell "never run" apart from "ran, found nothing".
    public StemResult? GetExistingStems(string songKey)
    {
        var dir = StemsDir(songKey);
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

    // Matches take-{section?}-{timestamp} filenames - the optional section
    // slug (letters/digits only) sits between the take-type prefix and the
    // fixed yyyyMMdd-HHmmss timestamp.
    private static readonly Regex TakeFilenamePattern = new(
        @"^(?:take|vtake|aitake|mixtake|mergetake)(?:-(?<section>[a-z0-9]+))?-\d{8}-\d{6}$", RegexOptions.Compiled);

    public List<RecordingFile> GetRecordings(string songKey)
    {
        var dir = Path.Combine(RecordingsDir, songKey);
        if (!Directory.Exists(dir)) return [];

        return Directory.GetFiles(dir)
            .Where(f => AudioExt.Concat([".webm", ".ogg"]).Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var m = TakeFilenamePattern.Match(name);
                var section = m.Success && m.Groups["section"].Success ? m.Groups["section"].Value : null;
                return new RecordingFile(Relative(f), name, new FileInfo(f).CreationTimeUtc, name.StartsWith("vtake"), name.StartsWith("aitake"), section);
            })
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    // recordings/{songKey}/take-{section?}-{timestamp}.webm (vtake- for video). A failed upload leaves no half file behind,
    // or it would show up as an unplayable take.
    public async Task<string> SaveRecordingAsync(string songKey, Stream content, string? contentType, string? section)
    {
        var dir = Path.Combine(RecordingsDir, SafeSegment(songKey));
        Directory.CreateDirectory(dir);
        var prefix = contentType?.Contains("video") == true ? "vtake" : "take";
        var sectionTag = string.IsNullOrWhiteSpace(section) ? "" : "-" + new string(section.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var dest = Path.Combine(dir, $"{prefix}{sectionTag}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.webm");
        await WriteAsync(dest, content);
        if (prefix == "take" && await LoudMp3Async(dest) is { } mp3) dest = mp3;
        return Relative(dest);
    }

    // The browser's webm has no length (players can't show the time or seek) and a mic a metre from a guitar comes in
    // around -47 dB (2026-10-14: the first takes sounded like nothing). ffmpeg makes it an mp3 with a length (also plays on an
    // iPhone) and turns the whole take up by ONE fixed amount, so its loudest point lands at -1.5 dB.
    // Not loudnorm: in one pass that is an automatic volume control, and it pumped the quiet seconds before playing up to
    // the same level as the guitar - the room's faint hiss became "loud background noise" (user, same day).
    // The webm is only removed when the mp3 is there; without ffmpeg the webm stays.
    private const double TargetPeakDb = -1.5, MaxGainDb = 30;

    // After the gain: rumble off below 70 Hz, steady room sound (hum, hiss, fan) reduced, and the pauses turned down 20 dB by
    // a soft gate - not to silence, so soft notes and a chord's decay stay. Tried on a real QuadCast take 2026-10-14
    // (user: knob at max, sitting close, still noise): pause -37 -> -48 dB, playing, soft spot and decay unchanged.
    // Stronger noise reduction took the pause lower but risks a "watery" guitar.
    private const string CleanUp = "highpass=f=70,afftdn=nr=20:nf=-40:tn=1,agate=threshold=0.035:ratio=4:attack=5:release=400:range=0.1";

    private static async Task<string?> LoudMp3Async(string webm)
    {
        var mp3 = Path.ChangeExtension(webm, ".mp3");
        try
        {
            var (_, measured) = await FfmpegAsync("-hide_banner", "-nostats", "-i", webm, "-vn", "-af", "volumedetect", "-f", "null", "-");
            var gain = PeakGainDb(measured);
            var (ok, _) = await FfmpegAsync("-y", "-i", webm, "-vn", "-af", FormattableString.Invariant($"volume={gain:0.0}dB,{CleanUp}"),
                "-ar", "44100", "-c:a", "libmp3lame", "-q:a", "2", mp3);
            if (!ok || !File.Exists(mp3) || new FileInfo(mp3).Length == 0) return null;
            File.Delete(webm);
            return mp3;
        }
        catch (Exception)
        {
            return null;   // ffmpeg missing (local Windows) - keep the webm
        }
    }

    /// <summary>The fixed gain that puts the take's loudest point at -1.5 dB, from ffmpeg volumedetect's "max_volume: -23.4 dB" (at most +30 dB).</summary>
    internal static double PeakGainDb(string volumedetectOutput)
    {
        var m = Regex.Match(volumedetectOutput, @"max_volume:\s*(-?[\d.]+) dB");
        if (!m.Success || !double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var max))
            return 0;
        return Math.Clamp(TargetPeakDb - max, 0, MaxGainDb);
    }

    private static async Task<(bool Ok, string Log)> FfmpegAsync(params string[] args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo("ffmpeg") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("ffmpeg did not start");
        var stdout = proc.StandardOutput.ReadToEndAsync();
        var stderr = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode == 0, await stderr + await stdout);
    }

    // Short "hum an idea" clips - deliberately apart from the takes, a sketch must never show up as a take.
    public List<SketchFile> GetSketches(string songKey)
    {
        var dir = Path.Combine(SketchesDir, songKey);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir)
            .Select(f => new SketchFile(Relative(f), Path.GetFileNameWithoutExtension(f), new FileInfo(f).CreationTimeUtc))
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    public async Task<string> SaveSketchAsync(string songKey, Stream content, string? contentType)
    {
        var dir = Path.Combine(SketchesDir, SafeSegment(songKey));
        Directory.CreateDirectory(dir);
        var ext = contentType?.Contains("webm") == true ? ".webm" : ".ogg";
        var dest = Path.Combine(dir, $"sketch-{DateTime.UtcNow:yyyyMMdd-HHmmss}{ext}");
        await WriteAsync(dest, content);
        return Relative(dest);
    }

    public bool Delete(string relativePath)
    {
        if (Resolve(relativePath) is not { } full) return false;
        try
        {
            File.Delete(full);
            return true;
        }
        catch (IOException)
        {
            // Most commonly the file is still open in an <audio> player on the page (Windows locks it).
            return false;
        }
    }

    private static string Transliterate(string s) => s
        .Replace("æ", "ae").Replace("ø", "oe").Replace("å", "aa")
        .Replace("Æ", "Ae").Replace("Ø", "Oe").Replace("Å", "Aa");

    public string SafeKey(string name) =>
        Transliterate(Path.GetFileNameWithoutExtension(name))
            .ToLower()
            .Replace(" ", "_")
            .Replace("-", "_");

    private string Relative(string full) => Path.GetRelativePath(root, full).Replace('\\', '/');

    // A song key from a URL must stay one folder name.
    private static string SafeSegment(string key) => new(key.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());

    private static async Task WriteAsync(string dest, Stream content)
    {
        try
        {
            await using var fs = File.Create(dest);
            await content.CopyToAsync(fs);
        }
        catch
        {
            if (File.Exists(dest)) File.Delete(dest);
            throw;
        }
    }
}
