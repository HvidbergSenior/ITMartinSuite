using System.Diagnostics;
using System.Linq;
using ImageMagick;
using ITMartin.Media.Contracts.Contracts.Runtime.Workflows;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Media.Application.Services.Steps.NormalizationStep;

public class ImageConverterService : IImageConverterService
{
    private readonly string _ffmpegPath;
    private readonly ILogger<ImageConverterService> _logger;

    private static readonly HashSet<string> ConvertibleExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".heic",
            ".heif",
            ".avif",
            // Confirmed 2026-09-03: MediaRulesWorkflowStep already flags
            // these as RequiresNormalization (non-canonical image formats),
            // but this allowlist never actually included them, so they were
            // exported to Billeder untouched instead of converted to .jpg.
            // MagickImage's constructor is format-agnostic - the same
            // ConvertWithMagick call used for HEIC handles TIFF/BMP natively.
            ".tif",
            ".tiff",
            ".bmp"
            // .gif deliberately NOT here - see ShouldKeepOriginal below.
        };

    public ImageConverterService(ILogger<ImageConverterService> logger)
    {
        _logger = logger;

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

    public bool TryGetSourceOrientation(string path, out ushort orientation) =>
        TryReadOrientationTag(path, out orientation);

    // Camera Model strings (EXIF tag 0x0110) known to write a meaningless
    // Orientation tag - matched by substring since some firmwares cram
    // several marketing names into one field (this Samsung writes "SAMSUNG
    // ES60 / VLUU ES60 / SAMSUNG SL105 / SAMSUNG ES63" as a single Model
    // value). Add to this list only once confirmed the same way ES60 was:
    // a real photo from that camera with Orientation=1 but physically wrong
    // pixels, cross-checked against an untouched original.
    private static readonly string[] OrientationUnreliableModelSubstrings =
    [
        "ES60", "SL105", "ES63",
    ];

    public bool IsFromOrientationUnreliableCamera(string path)
    {
        try
        {
            var exif = ImageMetadataReader.ReadMetadata(path)
                .OfType<ExifIfd0Directory>()
                .FirstOrDefault();

            if (exif == null || !exif.ContainsTag(ExifDirectoryBase.TagModel))
                return false;

            var model = exif.GetString(ExifDirectoryBase.TagModel) ?? string.Empty;
            return OrientationUnreliableModelSubstrings.Any(m =>
                model.Contains(m, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadOrientationTag(string path, out ushort orientation)
    {
        orientation = 0;
        try
        {
            var exif = ImageMetadataReader.ReadMetadata(path)
                .OfType<ExifIfd0Directory>()
                .FirstOrDefault();

            if (exif == null || !exif.ContainsTag(ExifDirectoryBase.TagOrientation))
                return false;

            orientation = (ushort)exif.GetInt32(ExifDirectoryBase.TagOrientation);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ShouldKeepOriginal(string path)
    {
        var name =
            Path.GetFileName(path)
                .ToLowerInvariant();

        var ext =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return ext is ".png" or ".jpg" or ".jpeg" or ".gif"
               // .gif specifically: converting to jpg would take a single
               // frame of what may be an animated image, destroying the
               // entire point of it - confirmed 2026-09-06, corrected after
               // briefly adding .gif to ConvertibleExtensions above instead.
               || name.Contains("screenshot")
               || name.Contains("meme");
    }

    public async Task<string?> ConvertToJpgAsync(
        string inputPath)
    {
        _logger.LogDebug("Converting {InputPath}", inputPath);

        if (!File.Exists(inputPath))
        {
            _logger.LogWarning("Cannot convert {InputPath} - file does not exist", inputPath);
            return null;
        }

        // =========================
        // KEEP ORIGINALS
        // =========================

        if (ShouldKeepOriginal(inputPath))
        {
            return BakeInOwnOrientationIfNeeded(inputPath) ?? inputPath;
        }

        if (!NeedsConversion(inputPath))
        {
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

        try
        {
            // Already normalized

            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            var ext = Path.GetExtension(inputPath).ToLowerInvariant();

            // Magick.NET bundles its own native ImageMagick binary per
            // platform (built with HEIF/AVIF delegate support) as a NuGet
            // dependency - no external CLI tool to install or be missing.
            // Replaces the old heif-convert (Linux-only, apt-get
            // libheif-examples) approach 2026-08-25 after it silently failed
            // every HEIC conversion on this Windows dev machine - heif-convert
            // was never installed here, so Process.Start threw, was caught,
            // and every HEIC file quietly exported unconverted instead of
            // failing loudly. AVIF still goes through ffmpeg (libaom handles
            // AV1 fine); only HEIC/HEIF moved to Magick.NET.
            if (ext is ".heic" or ".heif" or ".tif" or ".tiff" or ".bmp")
            {
                ConvertWithMagick(inputPath, outputPath);
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

            return outputPath;
        }
        catch (Exception ex)
        {
            // This is the "silently exported unconverted" failure mode found
            // 2026-08-25 - keep this at Warning (not swallowed to Debug), it's
            // the one thing about this method actually worth knowing when it
            // happens, since the caller falls back to the original file.
            _logger.LogWarning(ex, "Could not convert {InputPath} to JPG - keeping original file as-is", inputPath);

            return inputPath;
        }
    }

    private static void ConvertWithMagick(string inputPath, string outputPath)
    {
        using var image = new MagickImage(inputPath);
        image.Format = MagickFormat.Jpg;
        image.Write(outputPath);
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
                _logger.LogDebug("[ffmpeg] {Line}", e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                _logger.LogDebug("[ffmpeg] {Line}", e.Data);
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
    private string? BakeInOwnOrientationIfNeeded(string inputPath)
    {
        try
        {
            if (!TryReadOrientationTag(inputPath, out var orientation) || orientation <= 1)
                return null;

            var tempRoot = Path.Combine(Path.GetTempPath(), "ITMartinFileSorter", "images");
            System.IO.Directory.CreateDirectory(tempRoot);

            var outputPath = Path.Combine(tempRoot, Path.GetFileNameWithoutExtension(inputPath) + Path.GetExtension(inputPath));
            if (File.Exists(outputPath))
                return outputPath;

            using var image = Image.Load(inputPath);
            image.Mutate(x => x.AutoOrient());
            image.Metadata.ExifProfile?.RemoveValue(SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation);
            image.Save(outputPath);

            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not bake in orientation for {InputPath}", inputPath);
            return null;
        }
    }

    private void ApplyOriginalOrientation(
        string originalPath,
        string outputJpgPath)
    {
        try
        {
            if (!TryReadOrientationTag(originalPath, out var orientation) || orientation <= 1)
            {
                return;
            }

            using var image = Image.Load(outputJpgPath);

            image.Metadata.ExifProfile ??= new SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifProfile();
            image.Metadata.ExifProfile.SetValue(
                SixLabors.ImageSharp.Metadata.Profiles.Exif.ExifTag.Orientation,
                orientation);

            image.Mutate(x => x.AutoOrient());
            image.Metadata.ExifProfile = null;

            image.SaveAsJpeg(outputJpgPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not apply original orientation to {OutputJpgPath}", outputJpgPath);
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