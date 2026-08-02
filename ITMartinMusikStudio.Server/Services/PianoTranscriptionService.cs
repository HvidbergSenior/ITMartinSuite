using System.Diagnostics;

namespace ITMartinMusikStudio.Server.Services;

// Transcribes actual played notes (pitch + onset + duration) from an audio
// file to a MIDI file - the ByteDance high-resolution piano transcription
// model (CRNN, ~165MB checkpoint baked into the Dockerfile at build time so
// no runtime download is needed). Same external-Python-subprocess pattern as
// StemService/ChordDetectionService. Distinct from ChordDetectionService:
// that one only ever gives coarse chord labels: this gives individual notes,
// for actually copying a piano part key-by-key rather than just the chords.
public sealed class PianoTranscriptionService
{
    private readonly string _python;

    public bool IsAvailable { get; }

    public PianoTranscriptionService()
    {
        _python = DetectPython();
        IsAvailable = _python is not null;
    }

    public async Task<string> TranscribeAsync(string inputPath, string outputMidPath, CancellationToken ct = default)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), "musikstudio_piano_transcribe.py");
        if (!File.Exists(scriptPath))
            await File.WriteAllTextAsync(scriptPath, PythonScript, ct);

        Directory.CreateDirectory(Path.GetDirectoryName(outputMidPath)!);

        var psi = new ProcessStartInfo
        {
            FileName               = _python,
            Arguments              = $"\"{scriptPath}\" \"{inputPath}\" \"{outputMidPath}\"",
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
            throw new InvalidOperationException($"Node-transskription fejlede (kode {proc.ExitCode})\n{stderr.Trim()}");

        // piano_transcription_inference itself prints progress lines to stdout
        // ("Using cpu for inference.", "Segment 0 / 5", ...) ahead of our own
        // final count print - only the last non-empty line is the actual count.
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[^1] : "0";
    }

    // Loads audio itself via soundfile (bypasses audioread/ffmpeg entirely -
    // see feedback memory on why: audioread's ffmpeg backend needs a literal
    // "ffmpeg" binary on PATH, which the container's apt-installed ffmpeg
    // satisfies, but a plain soundfile read is simpler and has one less
    // moving part). Only handles WAV/FLAC-style PCM directly; the caller
    // (TranscribeAsync's Studio.razor call site) is expected to hand it
    // already-decoded audio via ffmpeg for compressed formats like mp3.
    private const string PythonScript = """
        import sys, subprocess, tempfile, os
        import soundfile as sf
        from piano_transcription_inference import PianoTranscription

        def main(input_path, output_mid):
            # Decode to 16kHz mono WAV first via ffmpeg (piano_transcription_inference
            # expects 16kHz) - covers mp3/m4a/whatever SourceFile actually is.
            tmp_wav = tempfile.mktemp(suffix='.wav')
            subprocess.run(
                ['ffmpeg', '-y', '-i', input_path, '-ar', '16000', '-ac', '1', tmp_wav],
                check=True, capture_output=True,
            )
            try:
                audio, sr = sf.read(tmp_wav, dtype='float32')
                transcriptor = PianoTranscription(device='cpu')
                result = transcriptor.transcribe(audio, output_mid)
                print(len(result.get('est_note_events', [])))
            finally:
                if os.path.exists(tmp_wav):
                    os.remove(tmp_wav)

        if __name__ == '__main__':
            main(sys.argv[1], sys.argv[2])
        """;

    private static string DetectPython()
    {
        foreach (var candidate in new[] { "python3", "python" })
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName               = candidate,
                    Arguments              = "-c \"import piano_transcription_inference; print('ok')\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                });
                // 45s not 10s - see StemService.cs's DetectPython for why
                // (numba/llvmlite cold-import JIT warmup, shared by demucs).
                p?.WaitForExit(45_000);
                if (p?.ExitCode == 0) return candidate;
            }
            catch { }
        }
        return null!;
    }
}
