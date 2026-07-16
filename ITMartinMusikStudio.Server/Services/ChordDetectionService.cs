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
