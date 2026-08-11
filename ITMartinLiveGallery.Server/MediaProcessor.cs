using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ITMartinLiveGallery.Server;

// Deliberately NOT the full FileSorter Package1/2/3 pipeline (see feedback
// memory on why: that one only runs manually, on a whole library at once,
// on Martin's own Windows PC - useless for "guest uploads a photo mid-event,
// it should appear in seconds"). This does the minimum needed per file:
// HEIC -> JPEG (iPhones default to HEIC, browsers can't display it) and a
// small thumbnail, both fast enough to run synchronously inside the upload
// request itself.
public static class MediaProcessor
{
    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".m4v", ".avi", ".webm" };
    private static readonly HashSet<string> HeicExt = new(StringComparer.OrdinalIgnoreCase)
        { ".heic", ".heif" };

    public static bool IsVideo(string filename) => VideoExt.Contains(Path.GetExtension(filename));

    // Returns (finalFilename, thumbFilename) - finalFilename may differ from
    // the upload if it was HEIC and got converted to JPEG.
    public static async Task<(string FinalFilename, string ThumbFilename)> ProcessAsync(
        string mediaDir, string thumbDir, string originalFilename, Stream content)
    {
        Directory.CreateDirectory(mediaDir);
        Directory.CreateDirectory(thumbDir);

        var ext = Path.GetExtension(originalFilename);
        var baseName = UniqueName(mediaDir, Path.GetFileNameWithoutExtension(originalFilename), ext);
        var savedPath = Path.Combine(mediaDir, baseName + ext);

        await using (var fs = File.Create(savedPath))
            await content.CopyToAsync(fs);

        if (IsVideo(originalFilename))
        {
            var thumbName = baseName + ".jpg";
            await GrabVideoFrameAsync(savedPath, Path.Combine(thumbDir, thumbName));
            return (baseName + ext, thumbName);
        }

        if (HeicExt.Contains(ext))
        {
            var jpgPath = Path.Combine(mediaDir, baseName + ".jpg");
            var converted = await ConvertHeicAsync(savedPath, jpgPath);
            if (converted)
            {
                File.Delete(savedPath);
                var thumbName = baseName + ".jpg";
                MakeThumbnail(jpgPath, Path.Combine(thumbDir, thumbName));
                return (baseName + ".jpg", thumbName);
            }
            // Conversion failed - keep the original HEIC (browsers won't
            // preview it, but nothing is lost) and fall through with no thumb.
            return (baseName + ext, "");
        }

        var finalThumbName = baseName + "_thumb.jpg";
        MakeThumbnail(savedPath, Path.Combine(thumbDir, finalThumbName));
        return (baseName + ext, finalThumbName);
    }

    private static string UniqueName(string dir, string baseName, string ext)
    {
        var candidate = baseName;
        var n = 1;
        while (File.Exists(Path.Combine(dir, candidate + ext)))
            candidate = $"{baseName}_{n++}";
        return candidate;
    }

    private static void MakeThumbnail(string sourcePath, string destPath)
    {
        try
        {
            using var image = Image.Load(sourcePath);
            image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(480, 480),
            }));
            image.SaveAsJpeg(destPath);
        }
        catch
        {
            // A thumbnail failure shouldn't fail the upload - the guest's
            // photo is already saved; the gallery just falls back to
            // showing no preview for that one item.
        }
    }

    private static async Task<bool> ConvertHeicAsync(string heicPath, string jpgPath)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "heif-convert",
                ArgumentList = { heicPath, jpgPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (proc is null) return false;
            await proc.WaitForExitAsync();
            return proc.ExitCode == 0 && File.Exists(jpgPath);
        }
        catch
        {
            return false;
        }
    }

    private static async Task GrabVideoFrameAsync(string videoPath, string thumbPath)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                ArgumentList = { "-y", "-i", videoPath, "-ss", "00:00:00.5", "-vframes", "1",
                                  "-vf", "scale=480:-1", thumbPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (proc is not null) await proc.WaitForExitAsync();
        }
        catch
        {
            // Same reasoning as MakeThumbnail - non-fatal.
        }
    }
}
