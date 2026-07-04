using System.Diagnostics;

namespace ITMartinMusikStudio.Server.Services;

public sealed class StemService
{
    private readonly string _python;

    public bool IsAvailable { get; }

    public StemService()
    {
        _python = DetectPython();
        IsAvailable = _python is not null;
    }

    public async Task<StemResult> SeparateAsync(
        string inputPath,
        string stemsOutputDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(stemsOutputDir);

        var tempOut = Path.Combine(stemsOutputDir, "_demucs_tmp");
        Directory.CreateDirectory(tempOut);

        var psi = new ProcessStartInfo
        {
            FileName               = _python,
            Arguments              = $"-m demucs --out \"{tempOut}\" \"{inputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start python");

        var stderrLines = new System.Collections.Generic.List<string>();
        _ = Task.Run(async () =>
        {
            while (!proc.StandardError.EndOfStream)
            {
                var line = await proc.StandardError.ReadLineAsync(ct);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    stderrLines.Add(line.Trim());
                    progress?.Report(line.Trim());
                }
            }
        }, ct);

        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0)
        {
            var detail = stderrLines.Count > 0 ? "\n" + string.Join("\n", stderrLines.TakeLast(5)) : "";
            throw new InvalidOperationException($"Demucs fejlede (kode {proc.ExitCode}){detail}");
        }

        // Demucs writes to {tempOut}/{model}/{filename}/ — find the folder with vocals.wav
        var stemFolder = Directory
            .EnumerateFiles(tempOut, "vocals.wav", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Stem files not found in Demucs output");

        // Move stems to flat output directory
        foreach (var name in new[] { "vocals", "drums", "bass", "other" })
        {
            var src  = Path.Combine(stemFolder, $"{name}.wav");
            var dest = Path.Combine(stemsOutputDir, $"{name}.wav");
            if (File.Exists(src))
                File.Move(src, dest, overwrite: true);
        }

        try { Directory.Delete(tempOut, recursive: true); } catch { }

        return new StemResult(
            Vocals: ExistsOrNull(stemsOutputDir, "vocals.wav"),
            Drums:  ExistsOrNull(stemsOutputDir, "drums.wav"),
            Bass:   ExistsOrNull(stemsOutputDir, "bass.wav"),
            Other:  ExistsOrNull(stemsOutputDir, "other.wav")
        );
    }

    private static string? ExistsOrNull(string dir, string file)
    {
        var path = Path.Combine(dir, file);
        return File.Exists(path) ? path : null;
    }

    private static string DetectPython()
    {
        foreach (var candidate in new[] { "python", "python3" })
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName               = candidate,
                    Arguments              = "-c \"import demucs; print('ok')\"",
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

public record StemResult(string? Vocals, string? Drums, string? Bass, string? Other);
