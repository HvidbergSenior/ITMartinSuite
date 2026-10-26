using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ITMartinUpload.Server.Services;

/// <summary>
/// Small previews for the "Det, I har sendt" list. A customer's phone photo is several
/// megabytes; showing fifty of them full size would make the page unusable on mobile data,
/// which is exactly the connection these customers are on.
/// </summary>
/// <remarks>
/// 2026-10-26: the page asks for many previews at once, and each one decoded the whole photo (a 48 MP phone photo is
/// ~200 MB in memory) - on the 1.7 GB NAS that got upload-web killed (exit 137) seconds after every start while
/// Annette's page was open. Now: decoded straight to preview size, one at a time, and huge files get the icon.
/// </remarks>
public static class ThumbnailFactory
{
    private const int MaxEdge = 320;
    /// <summary>Files above this get the generic icon - no preview is worth that much memory on the NAS.</summary>
    private const long MaxBytes = 40L * 1024 * 1024;

    private static readonly SemaphoreSlim OneAtATime = new(1, 1);

    public static byte[]? Create(string path)
    {
        try
        {
            if (new FileInfo(path).Length > MaxBytes) return null;
            OneAtATime.Wait();
            try
            {
                // TargetSize lets the decoder scale while reading (JPEG decodes at 1/2, 1/4, 1/8) instead of full size.
                using var image = Image.Load(new DecoderOptions { TargetSize = new Size(MaxEdge, MaxEdge) }, path);
                image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxEdge, MaxEdge),
                }));

                using var ms = new MemoryStream();
                image.Save(ms, new JpegEncoder { Quality = 72 });
                return ms.ToArray();
            }
            finally { OneAtATime.Release(); }
        }
        catch (Exception)
        {
            // A half-uploaded or unsupported file just gets the generic icon instead.
            return null;
        }
    }
}
