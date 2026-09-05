using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

// Mirrors VideoConverterService's shape/process-handling in this same folder -
// the audio equivalent was simply never built (m4a/wma/wav all get detected
// as non-canonical by CheckNormalized, but nothing ever converted them, so
// IsNormalized could never become true for an audio file - same class of gap
// as the RotationIsCorrect bug fixed 2026-09-05).
public class AudioConverterService : IAudioConverterService
{
    // A malformed/corrupt source file can make ffmpeg hang indefinitely
    // instead of erroring out - confirmed 2026-09-06 on a RicoAC .wma that
    // sat at near-zero CPU for minutes converting nothing, same failure mode
    // as the ffprobe hang fixed in VideoMetadataService 2026-09-05 (that fix
    // covers metadata reads; this covers the actual conversion call, a
    // separate process invocation). A single song should encode in well
    // under a minute on any real hardware, so 90s is generous, not tight.
    private static readonly TimeSpan FfmpegTimeout = TimeSpan.FromSeconds(90);

    private readonly string _ffmpegPath;

    public AudioConverterService()
    {
        _ffmpegPath = OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe")
            : "ffmpeg";
    }

    public async Task<string> ConvertToMp3Async(
        string inputPath,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var outputPath = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(inputPath)}.mp3");

        // -q:a 2 is libmp3lame's VBR quality scale (0 best/largest - 9
        // worst/smallest); 2 lands around 170-210kbps, effectively
        // transparent for the source material here (ripped/downloaded
        // library audio, not studio masters) without the fixed-bitrate
        // waste of -b:a.
        var args =
            $"-hide_banner -y -i \"{inputPath}\" " +
            "-vn -c:a libmp3lame -q:a 2 " +
            $"\"{outputPath}\"";

        await RunFfmpegAsync(args, cancellationToken);

        return outputPath;
    }

    private async Task RunFfmpegAsync(string args, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        // Started before waiting, not after - an unread stdout pipe can fill
        // its OS buffer and deadlock the process on its own, independent of
        // the hang this timeout guards against.
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = new CancellationTokenSource(FfmpegTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);

            if (cancellationToken.IsCancellationRequested) throw;
            throw new TimeoutException($"FFmpeg (audio) exceeded {FfmpegTimeout} and was killed - likely a corrupt/malformed source file, not a slow one.");
        }

        if (process.ExitCode != 0)
        {
            var stderr = await stdErrTask;
            throw new Exception($"FFmpeg (audio) failed with exit code {process.ExitCode}: {stderr}");
        }
    }
}
