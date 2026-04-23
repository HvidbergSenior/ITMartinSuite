using ITMartinImageProcessor.Application.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ITMartinImageProcessor.Infrastructure;

public class ImageSharpProcessor : IImageProcessor
{
    public async Task ProcessAsync(string inputPath, string outputPath)
    {
        using var image = await Image.LoadAsync(inputPath);

        image.Mutate(x => x
            .Resize(new ResizeOptions
            {
                Size = new Size(1500, 1500),
                Mode = ResizeMode.Crop
            })
            .Brightness(1.1f)
        );

        var dir = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);

        await image.SaveAsJpegAsync(outputPath);
    }
}