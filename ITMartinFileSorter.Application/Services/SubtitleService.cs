using System.Diagnostics;
using System.Text;

namespace ITMartinFileSorter.Application.Services;

public sealed class SubtitleService
{
    private const string WhisperExe =
        @"C:\Tools\Whisper\Release\whisper-cli.exe";

    private const string SmallModelPath =
        @"C:\Tools\Whisper\models\ggml-small.bin";

    private const string MediumModelPath =
        @"C:\Tools\Whisper\models\ggml-medium.bin";

    private static string FfmpegExe =>
        Path.Combine(
            AppContext.BaseDirectory,
            "ffmpeg",
            "ffmpeg.exe");

    public async Task<string?> GenerateDanishSubtitlesAsync(
        string videoPath,
        bool isLongFilm = false)
    {
        Console.WriteLine("SUBTITLE SERVICE STARTED");
        Debug.WriteLine("SUBTITLE SERVICE STARTED");
        Debug.WriteLine("===== SUBTITLE GENERATION STARTED =====");
        Debug.WriteLine($"Video file: {videoPath}");
        Debug.WriteLine($"Long film mode: {isLongFilm}");

        var wavPath = Path.ChangeExtension(videoPath, ".wav");
        var srtPath = $"{wavPath}.srt";
        var vttPath = Path.ChangeExtension(videoPath, ".da.vtt");

        Console.WriteLine("STEP 1");
        var audioOk = await ExtractAudioAsync(videoPath, wavPath);
        if (!audioOk)
        {
            Debug.WriteLine("ERROR: Audio extraction failed");
            return null;
        }

        var modelPath = isLongFilm
            ? MediumModelPath
            : SmallModelPath;

        var beamSize = isLongFilm ? 2 : 1;

        Debug.WriteLine($"Using model: {modelPath}");
        Debug.WriteLine($"Beam size: {beamSize}");

        Console.WriteLine("STEP 2");

        var whisperOk =
            await RunWhisperAsync(wavPath, modelPath, beamSize);

        Console.WriteLine("STEP 3");
        
        if (!whisperOk || !File.Exists(srtPath))
        {
            Debug.WriteLine("ERROR: Whisper failed or SRT file missing");
            return null;
        }

        Debug.WriteLine("SRT created successfully");

        ConvertSrtToVtt(srtPath, vttPath);

        Debug.WriteLine($"VTT created: {vttPath}");

        CleanupTempFiles(wavPath, srtPath);

        Debug.WriteLine("===== SUBTITLE GENERATION FINISHED =====");

        return vttPath;
    }

    private async Task<bool> ExtractAudioAsync(
        string videoPath,
        string wavPath)
    {
        Debug.WriteLine("Extracting audio with ffmpeg...");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = FfmpegExe,
                Arguments =
                    $"-y -i \"{videoPath}\" -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{wavPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        Debug.WriteLine(error);

        return File.Exists(wavPath);
    }

    private async Task<bool> RunWhisperAsync(
        string wavPath,
        string modelPath,
        int beamSize)
    {
        Console.WriteLine("STEP 2A - STARTING WHISPER");
        Console.WriteLine($"Model: {modelPath}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = WhisperExe,
                Arguments =
                    $"-m \"{modelPath}\" -f \"{wavPath}\" -l da -osrt --beam-size {beamSize}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        var seconds = 0;

        while (!process.HasExited)
        {
            await Task.Delay(5000);
            seconds += 5;
            Console.WriteLine($"WHISPER STILL RUNNING... {seconds} sec");
        }

        Console.WriteLine($"WHISPER FINISHED - Exit code: {process.ExitCode}");

        return process.ExitCode == 0;
    }
    private void ConvertSrtToVtt(
        string srtPath,
        string vttPath)
    {
        var lines = File.ReadAllLines(srtPath);

        using var writer =
            new StreamWriter(vttPath, false, Encoding.UTF8);

        writer.WriteLine("WEBVTT");
        writer.WriteLine();

        foreach (var line in lines)
        {
            writer.WriteLine(line.Replace(',', '.'));
        }
    }

    private void CleanupTempFiles(
        string wavPath,
        string srtPath)
    {
        try
        {
            if (File.Exists(wavPath))
                File.Delete(wavPath);

            if (File.Exists(srtPath))
                File.Delete(srtPath);
        }
        catch
        {
            Debug.WriteLine("Cleanup failed");
        }
    }
}