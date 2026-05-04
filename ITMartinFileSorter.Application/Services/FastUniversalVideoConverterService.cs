using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ITMartinFileSorter.Domain.Interfaces;

namespace ITMartinFileSorter.Application.Services;

public class FastUniversalVideoConverterService : IVideoConverter
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FastUniversalVideoConverterService()
    {
        _ffmpegPath = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        _ffprobePath = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

        Console.WriteLine($"[FFMPEG PATH] {_ffmpegPath}");
        Console.WriteLine($"[FFPROBE PATH] {_ffprobePath}");
    }

    public async Task<string?> ConvertToUniversalMp4Async(
        string inputPath,
        string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);

        var name = Path.GetFileNameWithoutExtension(inputPath);
        var finalOutputPath = Path.Combine(outputFolder, $"{name}.mp4");
        var tempOutputPath = Path.Combine(outputFolder, $"{name}.temp.mp4");

        var info = await GetCodecInfoAsync(inputPath);

        Console.WriteLine($"Video codec: {info.VideoCodec}");
        Console.WriteLine($"Audio codec: {info.AudioCodec}");

        try
        {
            if (CanCopy(inputPath, info))
            {
                Console.WriteLine("[VIDEO] Fast copy / rewrap");
                await RunFfmpegAsync(
                    $"-y -i \"{inputPath}\" -map_metadata 0 -c copy -movflags +faststart \"{tempOutputPath}\"");
            }
            else
            {
                Console.WriteLine("[VIDEO] Full re-encode");
                await RunFfmpegAsync(
                    $"-y -i \"{inputPath}\" -map_metadata 0 -c:v libx264 -preset veryfast -crf 18 -vf \"scale='min(1920,iw)':-2\" -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart \"{tempOutputPath}\"");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VIDEO] Fallback: {ex.Message}");

            await RunFfmpegAsync(
                $"-y -i \"{inputPath}\" -c:v libx264 -preset veryfast -crf 18 -c:a aac \"{tempOutputPath}\"");
        }

        await WaitForOutputReady(tempOutputPath);

        // Move first (safe)
        File.Move(tempOutputPath, finalOutputPath, true);

        // Delete original AFTER successful move
        if (!string.Equals(inputPath, finalOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            await SafeDeleteAsync(inputPath);
        }

        Console.WriteLine($"[VIDEO DONE] {finalOutputPath}");

        return finalOutputPath;
    }

    private bool CanCopy(string inputPath, CodecInfo info)
    {
        var ext = Path.GetExtension(inputPath).ToLowerInvariant();

        return ext is ".mp4" or ".mov"
               && info.VideoCodec == "h264"
               && info.AudioCodec == "aac";
    }

    private async Task<CodecInfo> GetCodecInfoAsync(string inputPath)
    {
        var args = $"-v quiet -print_format json -show_streams \"{inputPath}\"";

        using var process = StartProcess(_ffprobePath, args);

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        Console.WriteLine("===== FFPROBE DEBUG =====");
        Console.WriteLine(output);
        Console.WriteLine(error);

        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine("[FFPROBE ERROR] EMPTY OUTPUT");
            return new CodecInfo(null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(output);

            if (!doc.RootElement.TryGetProperty("streams", out var streams))
                return new CodecInfo(null, null);

            string? video = null;
            string? audio = null;

            foreach (var stream in streams.EnumerateArray())
            {
                if (!stream.TryGetProperty("codec_type", out var typeProp))
                    continue;

                if (!stream.TryGetProperty("codec_name", out var nameProp))
                    continue;

                var type = typeProp.GetString();
                var name = nameProp.GetString();

                Console.WriteLine($"[STREAM] {type} - {name}");

                if (type == "video" && video == null)
                    video = name;

                if (type == "audio" && audio == null)
                    audio = name;
            }

            return new CodecInfo(video, audio);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FFPROBE PARSE ERROR] {ex}");
            return new CodecInfo(null, null);
        }
    }

    private async Task RunFfmpegAsync(string args)
    {
        using var process = StartProcess(_ffmpegPath, args);

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        Console.WriteLine("===== FFMPEG DEBUG =====");
        Console.WriteLine(output);
        Console.WriteLine(error);

        if (process.ExitCode != 0)
            throw new Exception($"FFmpeg failed:\n{error}");
    }

    private Process StartProcess(string file, string args)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        })!;
    }

    private async Task SafeDeleteAsync(string path)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (!File.Exists(path))
                    return;

                File.Delete(path);

                Console.WriteLine($"[DELETE SUCCESS] {path}");
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DELETE RETRY {i}] {ex.Message}");
                await Task.Delay(300);
            }
        }

        Console.WriteLine($"[DELETE FAILED] {path}");
    }

    private async Task WaitForOutputReady(string path)
    {
        for (int i = 0; i < 20; i++)
        {
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length > 0)
                    return;
            }
            catch { }

            await Task.Delay(250);
        }
    }

    private record CodecInfo(string? VideoCodec, string? AudioCodec);
}