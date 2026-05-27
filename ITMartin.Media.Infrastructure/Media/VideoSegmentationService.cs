using System.Diagnostics;
using System.Text.RegularExpressions;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Infrastructure.Media;

public sealed class VideoSegmentationService
    : IVideoSegmentationService
{
    public async Task<List<MediaSegment>>
        DetectSegmentsAsync(
            string videoPath,
            CancellationToken cancellationToken = default)
    {
        var blackFrames =
            await DetectBlackFramesAsync(
                videoPath,
                cancellationToken);

        var duration =
            await GetDurationAsync(
                videoPath,
                cancellationToken);

        var segments =
            BuildSegments(
                blackFrames,
                duration);

        return segments;
    }
    public async Task GenerateSampleAsync(
        string inputPath,
        string outputPath,
        TimeSpan start,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var ffmpegPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "ffmpeg",
                "ffmpeg.exe");

        var args =
            $"-hide_banner -y " +
            $"-ss {start:hh\\:mm\\:ss} " +
            $"-i \"{inputPath}\" " +
            $"-t {duration:hh\\:mm\\:ss} " +
            "-c copy " +
            $"\"{outputPath}\"";

        var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = args,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
            };

        process.Start();

        await process.WaitForExitAsync(
            cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"FFmpeg split failed for {inputPath}");
        }
    }
    private async Task<List<BlackDetectResult>>
        DetectBlackFramesAsync(
            string videoPath,
            CancellationToken cancellationToken)
    {
        var ffmpegPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "ffmpeg",
                "ffmpeg.exe");

        var args =
            $"-i \"{videoPath}\" " +
            "-vf blackdetect=d=2:pix_th=0.10 " +
            "-an -f null -";

        var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = args,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
            };

        process.Start();

        var stderr =
            await process.StandardError
                .ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(
            cancellationToken);

        var results =
            new List<BlackDetectResult>();

        var regex =
            new Regex(
                @"black_start:(?<start>\d+(\.\d+)?)\s+black_end:(?<end>\d+(\.\d+)?)");

        foreach (Match match in regex.Matches(stderr))
        {
            var start =
                double.Parse(
                    match.Groups["start"].Value);

            var end =
                double.Parse(
                    match.Groups["end"].Value);

            results.Add(
                new BlackDetectResult
                {
                    Start =
                        TimeSpan.FromSeconds(start),

                    End =
                        TimeSpan.FromSeconds(end)
                });
        }

        return results;
    }

    private async Task<TimeSpan>
        GetDurationAsync(
            string videoPath,
            CancellationToken cancellationToken)
    {
        var ffprobePath =
            Path.Combine(
                AppContext.BaseDirectory,
                "ffmpeg",
                "ffprobe.exe");

        var args =
            $"-v error " +
            "-show_entries format=duration " +
            "-of default=noprint_wrappers=1:nokey=1 " +
            $"\"{videoPath}\"";

        var process =
            new Process
            {
                StartInfo =
                    new ProcessStartInfo
                    {
                        FileName = ffprobePath,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
            };

        process.Start();

        var output =
            await process.StandardOutput
                .ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(
            cancellationToken);

        if (!double.TryParse(
                output.Trim(),
                out var seconds))
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private List<MediaSegment>
        BuildSegments(
            List<BlackDetectResult> blackFrames,
            TimeSpan duration)
    {
        var result =
            new List<MediaSegment>();

        if (blackFrames.Count == 0)
        {
            result.Add(
                new MediaSegment
                {
                    Start = TimeSpan.Zero,
                    End = duration
                });

            return result;
        }

        TimeSpan current =
            TimeSpan.Zero;

        foreach (var blackFrame in blackFrames)
        {
            if (blackFrame.Start > current)
            {
                result.Add(
                    new MediaSegment
                    {
                        Start = current,
                        End = blackFrame.Start,
                        HasBlackFrameEnd = true
                    });
            }

            current = blackFrame.End;
        }

        if (current < duration)
        {
            result.Add(
                new MediaSegment
                {
                    Start = current,
                    End = duration
                });
        }

        return result;
    }
}