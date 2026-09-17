using System.Diagnostics;
using System.Text.Json;

namespace ITMartinMusikStudio.Server.Services;

public record ChordSegment(double StartSeconds, double EndSeconds, string Chord);

// Detects chords by actually listening to an audio file - the one real gap
// ChordAiService can't cover (it only recalls known songs by name/lyrics, or
// reads a photo, never audio). Same external-Python-subprocess pattern as
// StemService/Demucs. Uses librosa chroma + major/minor template matching
// (cosine similarity) rather than a trained model like autochord/madmom,
// because autochord's native `vamp` dependency needs a C++ compiler that
// isn't installed here - librosa ships prebuilt wheels, no compiler needed.
// Lower accuracy than a trained model, but a real, working starting point.
public sealed class ChordDetectionService
{
    private readonly string _python;

    public bool IsAvailable { get; }

    public ChordDetectionService()
    {
        _python = DetectPython();
        IsAvailable = _python is not null;
    }

    public async Task<List<ChordSegment>> DetectAsync(string inputPath, CancellationToken ct = default)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "musikstudio_chord_detect.py");
        if (!File.Exists(scriptPath))
            await File.WriteAllTextAsync(scriptPath, PythonScript, ct);

        var psi = new ProcessStartInfo
        {
            FileName               = _python,
            Arguments              = $"\"{scriptPath}\" \"{inputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start python");
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Akkordgenkendelse fejlede (kode {proc.ExitCode})\n{stderr.Trim()}");

        var raw = JsonSerializer.Deserialize<List<List<JsonElement>>>(stdout) ?? [];
        return raw.Select(r => new ChordSegment(r[0].GetDouble(), r[1].GetDouble(), r[2].GetString() ?? "")).ToList();
    }

    // Sung melody range from an isolated vocal track (demucs vocals.wav):
    // pyin pitch track, voiced frames only, 5th/95th percentile so a single
    // yelp or a low growl does not define the range. MIDI numbers.
    public async Task<(int LowMidi, int HighMidi)?> DetectMelodyRangeAsync(string vocalsPath, CancellationToken ct = default)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "musikstudio_melody_range.py");
        await File.WriteAllTextAsync(scriptPath, MelodyRangePythonScript, ct);

        var psi = new ProcessStartInfo
        {
            FileName               = _python,
            Arguments              = $"\"{scriptPath}\" \"{vocalsPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start python");
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = (await stdoutTask).Trim();
        _ = await stderrTask;
        var parts = stdout.Split(' ');
        return proc.ExitCode == 0 && parts.Length == 2 && int.TryParse(parts[0], out var lo) && int.TryParse(parts[1], out var hi) && hi > lo
            ? (lo, hi) : null;
    }

    private const string MelodyRangePythonScript = """
        import sys
        import numpy as np
        import librosa

        def main(path):
            y, sr = librosa.load(path, sr=22050, mono=True)
            f0, voiced, prob = librosa.pyin(y, fmin=librosa.note_to_hz('E2'), fmax=librosa.note_to_hz('C6'), sr=sr, frame_length=2048)
            f0 = f0[(voiced) & (prob > 0.5) & np.isfinite(f0)]
            if f0.size < 50:
                print("0 0"); return
            midi = librosa.hz_to_midi(f0)
            lo = int(round(np.percentile(midi, 5)))
            hi = int(round(np.percentile(midi, 95)))
            print(lo, hi)

        if __name__ == '__main__':
            main(sys.argv[1])
        """;

    public async Task<double?> DetectTempoAsync(string inputPath, CancellationToken ct = default)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "musikstudio_tempo_detect.py");
        if (!File.Exists(scriptPath))
            await File.WriteAllTextAsync(scriptPath, TempoPythonScript, ct);

        var psi = new ProcessStartInfo
        {
            FileName               = _python,
            Arguments              = $"\"{scriptPath}\" \"{inputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start python");
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = (await stdoutTask).Trim();

        return proc.ExitCode == 0 && double.TryParse(stdout, System.Globalization.CultureInfo.InvariantCulture, out var bpm)
            ? Math.Round(bpm, 1)
            : null;
    }

    private const string TempoPythonScript = """
        import sys
        import librosa

        def main(path):
            y, sr = librosa.load(path, sr=22050, mono=True)
            tempo, _ = librosa.beat.beat_track(y=y, sr=sr)
            # librosa 0.10+ returns tempo as a 1-element ndarray, not a scalar
            bpm = tempo.item() if hasattr(tempo, 'item') else tempo
            print(float(bpm))

        if __name__ == '__main__':
            main(sys.argv[1])
        """;

    // Renders detected segments as a readable, editable chord-chart block
    // (mm:ss timestamp per chord change) - same textarea format the rest of
    // the app already uses for chord charts, so the result drops straight in.
    public static string FormatAsChordChart(List<ChordSegment> segments) =>
        string.Join("\n", segments.Select(s =>
            $"{TimeSpan.FromSeconds(s.StartSeconds):mm\\:ss} {s.Chord}"));

    private const string PythonScript = """
        import sys, json
        import numpy as np
        import librosa

        def main(path):
            y, sr = librosa.load(path, sr=22050, mono=True)
            hop_length = 2048
            chroma = librosa.feature.chroma_cqt(y=y, sr=sr, hop_length=hop_length)
            chroma = librosa.decompose.nn_filter(chroma, aggregate=np.median, metric='cosine')

            notes = ['C','C#','D','D#','E','F','F#','G','G#','A','A#','B']
            templates = {}
            for i, n in enumerate(notes):
                maj = np.zeros(12); maj[[i, (i+4)%12, (i+7)%12]] = 1
                minr = np.zeros(12); minr[[i, (i+3)%12, (i+7)%12]] = 1
                templates[n] = maj
                templates[n+'m'] = minr

            names = list(templates.keys())
            T = np.array([templates[n] for n in names])
            T = T / np.linalg.norm(T, axis=1, keepdims=True)

            C = chroma.T
            norms = np.linalg.norm(C, axis=1, keepdims=True)
            norms[norms == 0] = 1
            Cn = C / norms

            scores = Cn @ T.T
            best = np.argmax(scores, axis=1)
            conf = np.max(scores, axis=1)

            times = librosa.frames_to_time(np.arange(chroma.shape[1]), sr=sr, hop_length=hop_length)

            segments = []
            cur_chord = None
            cur_start = 0.0
            for i, t in enumerate(times):
                chord = names[best[i]] if conf[i] > 0.5 else None
                if chord != cur_chord:
                    if cur_chord is not None:
                        segments.append([cur_start, float(t), cur_chord])
                    cur_chord = chord
                    cur_start = float(t)
            if cur_chord is not None:
                segments.append([cur_start, float(times[-1]), cur_chord])

            merged = []
            for seg in segments:
                if merged and seg[1] - seg[0] < 1.0:
                    merged[-1][1] = seg[1]
                else:
                    merged.append(seg)

            print(json.dumps(merged))

        if __name__ == '__main__':
            main(sys.argv[1])
        """;

    private static string DetectPython()
    {
        foreach (var candidate in new[] { "python", "python3" })
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName               = candidate,
                    Arguments              = "-c \"import librosa; print('ok')\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                });
                p?.WaitForExit(10_000);
                if (p?.ExitCode == 0) return candidate;
            }
            catch { }
        }
        return null!;
    }
}

// Key from a detected chord timeline: the major/minor key whose diatonic
// chords cover the most playing time, tie broken by the opening chord.
public static class KeyGuesser
{
    private static readonly string[] Notes = ["C","C#","D","D#","E","F","F#","G","G#","A","A#","B"];
    private static readonly int[] MajorSteps = [0, 2, 4, 5, 7, 9, 11];
    private static readonly bool[] MajorQualityMinor = [false, true, true, false, false, true, true]; // I ii iii IV V vi vii°(treated minor)

    public static string? Guess(IReadOnlyList<ChordSegment> segments)
    {
        if (segments.Count == 0) return null;
        var weight = new Dictionary<(int Root, bool Minor), double>();
        foreach (var s in segments)
        {
            var (root, minor) = Parse(s.Chord);
            if (root < 0) continue;
            weight[(root, minor)] = weight.GetValueOrDefault((root, minor)) + Math.Max(0.1, s.EndSeconds - s.StartSeconds);
        }
        if (weight.Count == 0) return null;

        double Score(int tonic, bool minorKey)
        {
            // Minor key = relative major's chord set, shifted.
            var majorTonic = minorKey ? (tonic + 3) % 12 : tonic;
            double sum = 0;
            for (var d = 0; d < 7; d++)
            {
                var r = (majorTonic + MajorSteps[d]) % 12;
                sum += weight.GetValueOrDefault((r, MajorQualityMinor[d]));
            }
            // Bias toward keys whose tonic chord actually appears, esp. as the first chord.
            sum += weight.GetValueOrDefault((tonic, minorKey)) * 0.5;
            var (fr, fm) = Parse(segments[0].Chord);
            if (fr == tonic && fm == minorKey) sum += 3;
            return sum;
        }

        var best = Enumerable.Range(0, 12).SelectMany(t => new[] { (t, false), (t, true) })
            .MaxBy(k => Score(k.Item1, k.Item2));
        return Notes[best.Item1] + (best.Item2 ? "m" : "");
    }

    private static (int Root, bool Minor) Parse(string chord)
    {
        if (string.IsNullOrEmpty(chord)) return (-1, false);
        var name = chord.Split('/')[0];
        var len = name.Length > 1 && name[1] is '#' or 'b' ? 2 : 1;
        var root = name[..len].Replace("Db", "C#").Replace("Eb", "D#").Replace("Gb", "F#").Replace("Ab", "G#").Replace("Bb", "A#");
        var idx = Array.IndexOf(Notes, root);
        var rest = name[len..];
        var minor = rest.StartsWith('m') && !rest.StartsWith("maj");
        return (idx, minor);
    }
}
