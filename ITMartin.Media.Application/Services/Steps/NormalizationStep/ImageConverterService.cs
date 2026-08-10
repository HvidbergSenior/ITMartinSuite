using System.Diagnostics;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public class ImageConverterService : IImageConverterService
{
    private readonly string _ffmpegPath;

    private static readonly HashSet<string> ConvertibleExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".heic",
            ".heif",
            ".avif"
        };

    public ImageConverterService()
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
        }
        else
        {
            // Docker / Linux / Synology

            _ffmpegPath = "ffmpeg";
        }

       
    }

    public bool NeedsConversion(string path)
    {
        var ext =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return ConvertibleExtensions.Contains(ext);
    }

    public bool ShouldKeepOriginal(string path)
    {
        var name =
            Path.GetFileName(path)
                .ToLowerInvariant();

        var ext =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return ext is ".png" or ".jpg" or ".jpeg"
               || name.Contains("screenshot")
               || name.Contains("meme");
    }

    public async Task<string?> ConvertToJpgAsync(
        string inputPath)
    {
        Console.WriteLine(
            "===== IMAGE DEBUG START =====");

        Console.WriteLine(
            $"Input path: {inputPath}");

        if (!File.Exists(inputPath))
        {
            Console.WriteLine(
                "Input file missing");

            return null;
        }

        // =========================
        // KEEP ORIGINALS
        // =========================

        if (ShouldKeepOriginal(inputPath))
        {
            Console.WriteLine(
                "Keeping original");

            return BakeInOwnOrientationIfNeeded(inputPath) ?? inputPath;
        }

        if (!NeedsConversion(inputPath))
        {
            Console.WriteLine(
                "No conversion needed");

            return BakeInOwnOrientationIfNeeded(inputPath) ?? inputPath;
        }

        // =========================
        // TEMP NORMALIZED FOLDER
        // =========================

        var tempRoot =
            Path.Combine(
                Path.GetTempPath(),
                "ITMartinFileSorter",
                "images");

        System.IO.Directory.CreateDirectory(
            tempRoot);

        // =========================
        // SAFE OUTPUT NAME
        // =========================

        var fileName =
            Path.GetFileNameWithoutExtension(
                inputPath);

        var outputPath =
            Path.Combine(
                tempRoot,
                $"{fileName}.jpg");

        Console.WriteLine(
            $"Output path: {outputPath}");

        try
        {
            // Already normalized

            if (File.Exists(outputPath))
            {
                Console.WriteLine(
                    "Already normalized");

                return outputPath;
            }

            var ext = Path.GetExtension(inputPath).ToLowerInvariant();

            // ffmpeg (from plain apt-get, no --enable-libheif) cannot decode
            // HEIC/HEIF at all - it mis-detects them as an MP4-family container
            // and fails with "moov atom not found" every time. heif-convert
            // (libheif-examples) actually decodes the HEIF box structure.
            // AVIF is a different codec (AV1) that ffmpeg's libaom build
            // handles fine, so that one keeps using ffmpeg.
            if (ext is ".heic" or ".heif")
            {
                await ConvertWithHeifConvert(
                    inputPath,
                    outputPath);
            }
            else
            {
                await ConvertWithFfmpeg(
                    inputPath,
                    outputPath);
            }

            if (!File.Exists(outputPath))
            {
                throw new Exception(
                    "JPG not created");
            }

            // heif-convert/ffmpeg decode the raw pixel grid but neither one
            // carries over the original's EXIF Orientation tag, so an iPhone
            // photo taken sideways/upside-down comes out sideways/upside-down
            // with nothing left to correct it downstream (thumbnails included).
            ApplyOriginalOrientation(
                inputPath,
                outputPath);

            CopyDates(
                inputPath,
                outputPath);

            Console.WriteLine(
                "===== IMAGE DEBUG END =====");

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[IMAGE CONVERT ERROR] {ex}");

            return inputPath;
        }
    }

    private static async Task ConvertWithHeifConvert(
        string inputPath,
        string outputPath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "heif-convert",
                Arguments = $"\"{inputPath}\" \"{outputPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        var tcs = new TaskCompletionSource<int>();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Console.WriteLine(e.Data);
            }
        };

        process.Exited += (_, _) =>
        {
            tcs.TrySetResult(process.ExitCode);
        };

        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exitCode = await tcs.Task;

        if (exitCode != 0)
        {
            throw new Exception(
                $"heif-convert failed: {exitCode}");
        }
    }

    private async Task ConvertWithFfmpeg(
        string inputPath,
        string outputPath)
    {
        if (OperatingSystem.IsWindows() &&
            !File.Exists(_ffmpegPath))
        {
            throw new FileNotFoundException(
                "FFmpeg not found",
                _ffmpegPath);
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments =
                    $"-y -i \"{inputPath}\" -frames:v 1 \"{outputPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        var tcs = new TaskCompletionSource<int>();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                Console.WriteLine(e.Data);
            }
        };

        process.Exited += (_, _) =>
        {
            tcs.TrySetResult(process.ExitCode);
        };

        process.Start();

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var exitCode =
            await tcs.Task;
        
        if (exitCode != 0)
        {
            throw new Exception(
                $"FFmpeg failed: {exitCode}");
        }
    }

    // Ordinary JPEGs/PNGs pass through untouched today - they keep whatever
    // EXIF Orientation tag the camera/phone wrote, and nothing downstream
    // (thumbnails, the gallery viewer) rotates based on it. Bake it into the
    // pixels here, once, at import time, so every consumer just sees an
    // upright image. Cheap tag-only read first so the common case (already
    // orientation 1, i.e. no camera metadata or already upright) costs
    // nothing - only orientations 2-8 pay for a decode+re-encode.
    private static string? BakeInOwnOrientationIfNeeded(string inputPath)
    {
        try
        {
            var exif = ImageMetadataReader.ReadMetadata(inputPath)
                .OfType<ExifIfd0Directory>()
                .FirstOrDefault();

            if (exif == null || !exif.ContainsTag(ExifDirectoryBase.TagOrientation))
                return null;

            var orientation = (ushort)exif.GetInt32(ExifDirectoryBase.TagOrientation);
            if (orientation <= 1)
                return null;

            var tempRoot = Path.Combine(Path.GetTempPath(), "ITMartinFileSorter", "images");
            System.IO.Directory.CreateDirectory(tempRoot);

            var outputPath = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(inputPath) + Path.GetExtension(inputPath));
            if (File.Exists(outputPath))
                return outputPath;

            using var image = Image.Load(inputPath);
            image.Mutate(x => x.AutoOrient());
            image.Metadata.ExifProfile?.RemoveValue(ExifTag.Orientation);
            image.Save(outputPath);

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTO-ROTATE ERROR] {inputPath}: {ex.Message}");
            return null;
        }
    }

    private static void ApplyOriginalOrientation(
        string originalPath,
        string outputJpgPath)
    {
        try
        {
            var exif = ImageMetadataReader.ReadMetadata(originalPath)
                .OfType<ExifIfd0Directory>()
                .FirstOrDefault();

            if (exif == null ||
                !exif.ContainsTag(ExifDirectoryBase.TagOrientation))
            {
                return;
            }

            var orientation = (ushort)exif.GetInt32(
                ExifDirectoryBase.TagOrientation);

            if (orientation <= 1)
            {
                return;
            }

            using var image = Image.Load(outputJpgPath);

            image.Metadata.ExifProfile ??= new ExifProfile();
            image.Metadata.ExifProfile.SetValue(
                ExifTag.Orientation,
                orientation);

            image.Mutate(x => x.AutoOrient());
            image.Metadata.ExifProfile = null;

            image.SaveAsJpeg(outputJpgPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"[ORIENTATION FIX ERROR] {ex.Message}");
        }
    }

    private void CopyDates(
        string inputPath,
        string outputPath)
    {
        var created =
            File.GetCreationTime(inputPath);

        var modified =
            File.GetLastWriteTime(inputPath);

        File.SetCreationTime(
            outputPath,
            created);

        File.SetLastWriteTime(
            outputPath,
            modified);
    }
}