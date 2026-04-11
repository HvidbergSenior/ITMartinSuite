using System.Diagnostics;
using System.Text.Json;

namespace ITMartinFileSorter.Application.Services;

public class FastUniversalVideoConverterService
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FastUniversalVideoConverterService()
    {
        var ffmpegFolder = Path.Combine(AppContext.BaseDirectory, "ffmpeg");

        _ffmpegPath = Path.Combine(ffmpegFolder, "ffmpeg.exe");
        _ffprobePath = Path.Combine(ffmpegFolder, "ffprobe.exe");
    }

    public async Task<string?> ConvertToUniversalMp4Async(
        string inputPath,
        string outputFolder)
    {
        if (!File.Exists(_ffmpegPath))
            throw new FileNotFoundException("FFmpeg not found", _ffmpegPath);

        if (!File.Exists(_ffprobePath))
            throw new FileNotFoundException("FFprobe not found", _ffprobePath);

        Directory.CreateDirectory(outputFolder);

        var name = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = Path.Combine(outputFolder, $"{name}.mp4");

        var info = await GetCodecInfoAsync(inputPath);

        Console.WriteLine($"Video codec: {info.VideoCodec}");
        Console.WriteLine($"Audio codec: {info.AudioCodec}");

        try
        {
            if (CanCopy(inputPath, info))
            {
                Console.WriteLine("[VIDEO] Fast copy / rewrap");
                await CopyAsync(inputPath, outputPath);
            }
            else
            {
                Console.WriteLine("[VIDEO] Full re-encode");
                await ReencodeAsync(inputPath, outputPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VIDEO] Copy failed: {ex.Message}");
            Console.WriteLine("[VIDEO] Falling back to full re-encode");

            await ReencodeAsync(inputPath, outputPath);
        }

        await WaitForOutputReady(outputPath);
        CopyDates(inputPath, outputPath);
        TryDeleteOriginal(inputPath, outputPath);
        return outputPath;
    }

    private bool CanCopy(string inputPath, CodecInfo info)
    {
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();

        var containerOk = ext is ".mp4" or ".mov";
        var videoOk = info.VideoCodec is "h264";
        var audioOk = info.AudioCodec is "aac";

        return containerOk && videoOk && audioOk;
    }

    private async Task<CodecInfo> GetCodecInfoAsync(string inputPath)
    {
        var arguments =
            $"-v quiet -print_format json -show_streams \"{inputPath}\"";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffprobePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var json = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        using var doc = JsonDocument.Parse(json);

        string? videoCodec = null;
        string? audioCodec = null;

        if (!doc.RootElement.TryGetProperty("streams", out var streams))
            return new CodecInfo(null, null);

        foreach (var stream in streams.EnumerateArray())
        {
            if (!stream.TryGetProperty("codec_type", out var codecTypeElement))
                continue;

            if (!stream.TryGetProperty("codec_name", out var codecNameElement))
                continue;

            var codecType = codecTypeElement.GetString();
            var codecName = codecNameElement.GetString();

            if (codecType == "video" && videoCodec == null)
                videoCodec = codecName;

            if (codecType == "audio" && audioCodec == null)
                audioCodec = codecName;
        }

        Console.WriteLine($"VIDEO CODEC: {videoCodec}");
        Console.WriteLine($"AUDIO CODEC: {audioCodec}");

        return new CodecInfo(videoCodec, audioCodec);
    }

    private async Task CopyAsync(string inputPath, string outputPath)
    {
        await RunFfmpegAsync(
            $"-y -i \"{inputPath}\" " +
            "-c copy " +
            "-movflags +faststart " +
            $"\"{outputPath}\"");
    }

    private async Task ReencodeAsync(string inputPath, string outputPath)
    {
        await RunFfmpegAsync(
            $"-y -i \"{inputPath}\" " +
            "-c:v libx264 " +
            "-preset veryfast " +
            "-crf 18 " +
            "-vf \"scale='min(1920,iw)':-2\" " +
            "-pix_fmt yuv420p " +
            "-c:a aac " +
            "-b:a 192k " +
            "-movflags +faststart " +
            $"\"{outputPath}\"");
    }

    private async Task RunFfmpegAsync(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = arguments,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        Console.WriteLine(output);
        Console.WriteLine(error);

        if (process.ExitCode != 0)
            throw new Exception($"FFmpeg failed:\n{error}");
    }

    private void CopyDates(string inputPath, string outputPath)
    {
        var created = File.GetCreationTime(inputPath);
        var modified = File.GetLastWriteTime(inputPath);

        if (created.Year < 2000)
            created = modified;

        File.SetCreationTime(outputPath, created);
        File.SetLastWriteTime(outputPath, modified);
    }

    private async Task WaitForOutputReady(string outputPath)
    {
        for (int i = 0; i < 20; i++)
        {
            try
            {
                using var stream = File.Open(
                    outputPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

                if (stream.Length > 0)
                    return;
            }
            catch
            {
                // still locked
            }

            await Task.Delay(250);
        }
    }
    private void TryDeleteOriginal(string inputPath, string outputPath)
    {
        if (!File.Exists(outputPath))
            return;

        if (string.Equals(inputPath, outputPath,
                StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            File.Delete(inputPath);

            Console.WriteLine($"[VIDEO ORIGINAL DELETED] {inputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VIDEO DELETE FAILED] {ex}");
        }
    }
    private record CodecInfo(string? VideoCodec, string? AudioCodec);
}