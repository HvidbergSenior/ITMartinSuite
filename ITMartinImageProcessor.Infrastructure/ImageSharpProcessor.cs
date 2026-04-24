using ITMartinImageProcessor.Domain.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace ITMartinImageProcessor.Infrastructure;

public class ImageSharpProcessor : IImageProcessor
{
    public async Task ProcessAsync(string inputPath, string outputPath)
    {
        using var image = await Image.LoadAsync<Rgba32>(inputPath);

        const int size = 1200;

        // 🔥 Resize WITHOUT cropping (keeps full object)
        image.Mutate(x => x
            .AutoOrient()
            .Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Max // 👈 keeps full image
            })
        );

        // 🔥 Create white canvas (webshop style)
        using var canvas = new Image<Rgba32>(size, size, Color.White);

        // center the image on canvas
        var xPos = (size - image.Width) / 2;
        var yPos = (size - image.Height) / 2;

        canvas.Mutate(ctx =>
        {
            // subtle shadow effect
            ctx.DrawImage(image.Clone(i => i
                .Brightness(0.9f)
                .GaussianBlur(10)), new Point(xPos + 10, yPos + 10), 0.3f);

            // main image
            ctx.DrawImage(image, new Point(xPos, yPos), 1f);

            // slight polish
            ctx.Brightness(1.03f);
            ctx.Contrast(1.05f);
        });

        var dir = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);

        await canvas.SaveAsJpegAsync(outputPath, new JpegEncoder
        {
            Quality = 90
        });
    }
}