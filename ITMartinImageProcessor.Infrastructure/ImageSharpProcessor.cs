using ITMartinImageProcessor.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ITMartinImageProcessor.Infrastructure;

public class ImageSharpProcessor : IImageProcessor
{
    public async Task ProcessAsync(string inputPath, string outputPath)
    {
        using var image = await Image.LoadAsync<Rgba32>(inputPath);

        const int size = 1200;

        image.Mutate(x => x
            .AutoOrient()
            .Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Max
            }));

        var dir = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);

        await image.SaveAsJpegAsync(outputPath, new JpegEncoder
        {
            Quality = 90
        });
    }
}