using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ITMartin.Media.Interfaces;

namespace ITMartin.Media.Application.Services;

public class VideoConverterService : IVideoConverterService
{
    private readonly string _ffmpegPath;

    private readonly string _ffprobePath;

    public VideoConverterService()
    {
        if (OperatingSystem.IsWindows())
        {
            var ffmpegFolder =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "ffmpeg");

            _ffmpegPath =
                Path.Combine(
                    ffmpegFolder,
                    "ffmpeg.exe");

            _ffprobePath =
                Path.Combine(
                    ffmpegFolder,
                    "ffprobe.exe");
        }
        else
        {
            _ffmpegPath = "ffmpeg";
            _ffprobePath = "ffprobe";
        }

        Console.WriteLine(
            $"[FFMPEG] {_ffmpegPath}");

        Console.WriteLine(
            $"[FFPROBE] {_ffprobePath}");
    }

    public async Task<string?> ConvertToUniversalMp4Async(
        string inputPath,
        string outputFolder)
    {
        if (!File.Exists(inputPath))
        {
            Console.WriteLine(
                $"[VIDEO] Missing: {inputPath}");

            return null;
        }

        var tempRoot =
            Path.Combine(
                Path.GetTempPath(),
                "ITMartinFileSorter",
                "videos");

        Directory.CreateDirectory(
            tempRoot);

        // CLEANER FILE NAME

        var baseName =
            BuildCleanName(
                Path.GetFileNameWithoutExtension(
                    inputPath));

        var uniqueName =
            $"{baseName}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        var finalOutput =
            Path.Combine(
                tempRoot,
                $"{uniqueName}.mp4");

        var tempOutput =
            Path.Combine(
                tempRoot,
                $"{uniqueName}.tmp.mp4");

        Console.WriteLine(
            $"[VIDEO INPUT] {inputPath}");

        Console.WriteLine(
            $"[VIDEO OUTPUT] {finalOutput}");

        var info =
            await GetCodecInfoAsync(
                inputPath);

        Console.WriteLine(
            $"[VIDEO CODEC] {info.VideoCodec}");

        Console.WriteLine(
            $"[AUDIO CODEC] {info.AudioCodec}");

        try
        {
            var shouldCopy =
                CanCopy(
                    inputPath,
                    info);

            if (shouldCopy)
            {
                Console.WriteLine(
                    "[VIDEO] Rewrap");

                await RunFfmpegAsync(
                    $"-y -i \"{inputPath}\" " +
                    "-map_metadata 0 " +
                    "-c copy " +
                    "-movflags +faststart " +
                    $"\"{tempOutput}\"");
            }
            else
            {
                Console.WriteLine(
                    "[VIDEO] Re-encode");

                await RunFfmpegAsync(
                    $"-y -i \"{inputPath}\" " +
                    "-map_metadata 0 " +
                    "-c:v libx264 " +
                    "-preset medium " +
                    "-crf 20 " +
                    "-pix_fmt yuv420p " +
                    "-vf \"scale='min(1920,iw)':-2\" " +
                    "-c:a aac " +
                    "-b:a 192k " +
                    "-movflags +faststart " +
                    $"\"{tempOutput}\"");
            }

            await WaitForOutputReady(
                tempOutput);

            if (!File.Exists(tempOutput))
            {
                throw new Exception(
                    "Output file missing");
            }

            File.Move(
                tempOutput,
                finalOutput,
                true);

            Console.WriteLine(
                $"[VIDEO DONE] {finalOutput}");

            return finalOutput;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[VIDEO ERROR] {ex}");

            try
            {
                if (File.Exists(tempOutput))
                {
                    File.Delete(tempOutput);
                }
            }
            catch
            {
            }

            return inputPath;
        }
    }

    private bool CanCopy(
        string inputPath,
        CodecInfo info)
    {
        var ext =
            Path.GetExtension(inputPath)
                .ToLowerInvariant();

        // AVI should NEVER be copied

        if (ext == ".avi")
        {
            return false;
        }

        return ext is ".mp4" or ".mov"
               && info.VideoCodec == "h264"
               && info.AudioCodec == "aac";
    }

    private async Task<CodecInfo> GetCodecInfoAsync(
        string inputPath)
    {
        try
        {
            var args =
                $"-v quiet -print_format json -show_streams \"{inputPath}\"";

            using var process =
                StartProcess(
                    _ffprobePath,
                    args);

            var output =
                await process.StandardOutput
                    .ReadToEndAsync();

            await process.WaitForExitAsync();

            if (string.IsNullOrWhiteSpace(output))
            {
                return new CodecInfo(
                    null,
                    null);
            }

            using var doc =
                JsonDocument.Parse(output);

            if (!doc.RootElement.TryGetProperty(
                    "streams",
                    out var streams))
            {
                return new CodecInfo(
                    null,
                    null);
            }

            string? video = null;
            string? audio = null;

            foreach (var stream in streams.EnumerateArray())
            {
                var type =
                    stream.GetProperty("codec_type")
                        .GetString();

                var codec =
                    stream.GetProperty("codec_name")
                        .GetString();

                if (type == "video" &&
                    video == null)
                {
                    video = codec;
                }

                if (type == "audio" &&
                    audio == null)
                {
                    audio = codec;
                }
            }

            return new CodecInfo(
                video,
                audio);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[FFPROBE ERROR] {ex}");

            return new CodecInfo(
                null,
                null);
        }
    }

    private async Task RunFfmpegAsync(
        string args)
    {
        using var process =
            StartProcess(
                _ffmpegPath,
                args);

        var errorBuilder =
            new StringBuilder();

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Console.WriteLine(
                    $"[FFMPEG] {e.Data}");

                errorBuilder.AppendLine(e.Data);
            }
        };

        process.BeginErrorReadLine();

        var waitTask =
            process.WaitForExitAsync();

        var timeoutTask =
            Task.Delay(
                TimeSpan.FromMinutes(15));

        var completed =
            await Task.WhenAny(
                waitTask,
                timeoutTask);

        if (completed == timeoutTask)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
            }

            throw new Exception(
                "FFmpeg timeout");
        }

        if (process.ExitCode != 0)
        {
            throw new Exception(
                errorBuilder.ToString());
        }
    }

    private Process StartProcess(
        string file,
        string args)
    {
        var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = file,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
            };

        process.Start();

        return process;
    }

    private async Task WaitForOutputReady(
        string path)
    {
        for (int i = 0; i < 40; i++)
        {
            try
            {
                using var stream =
                    File.Open(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);

                if (stream.Length > 0)
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(250);
        }

        throw new Exception(
            "Output file never became ready");
    }

    private static string BuildCleanName(
        string value)
    {
        value =
            Regex.Replace(
                value,
                @"[^a-zA-Z0-9_\- ]",
                "");

        value =
            Regex.Replace(
                value,
                @"\s+",
                "_");

        if (value.Length > 40)
        {
            value = value[..40];
        }

        return value.Trim('_');
    }

    private record CodecInfo(
        string? VideoCodec,
        string? AudioCodec);
}