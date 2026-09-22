using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ITMartinUpload.Server.Services;

/// <summary>
/// Small previews for the "Det, I har sendt" list. A customer's phone photo is several
/// megabytes; showing fifty of them full size would make the page unusable on mobile data,
/// which is exactly the connection these customers are on.
/// </summary>
public static class ThumbnailFactory
{
    private const int MaxEdge = 320;

    public static byte[]? Create(string path)
    {
        try
        {
            using var image = Image.Load(path);
            image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxEdge, MaxEdge),
            }));

            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder { Quality = 72 });
            return ms.ToArray();
        }
        catch (Exception)
        {
            // A half-uploaded or unsupported file just gets the generic icon instead.
            return null;
        }
    }
}
