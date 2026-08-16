using System.Numerics;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Media.Infrastructure.Images;

public sealed class PerceptualHashService : IPerceptualHashService
{
    private readonly ILogger<PerceptualHashService> _logger;

    public PerceptualHashService(ILogger<PerceptualHashService> logger)
    {
        _logger = logger;
    }

    public async Task<ulong?> ComputeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await Image.LoadAsync<L8>(imagePath, cancellationToken);

            // Same orientation handling as thumbnail generation - without
            // AutoOrient, a sideways EXIF-tagged photo and its already-upright
            // recompressed copy would hash completely differently even though
            // they show the same picture.
            image.Mutate(x => x
                .AutoOrient()
                // 9x8 so each row has 8 left-to-right pixel comparisons -> 64 bits total.
                .Resize(new ResizeOptions { Size = new Size(9, 8), Mode = ResizeMode.Stretch }));

            ulong hash = 0;
            ulong bit = 1UL << 63;

            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    var left = image[x, y].PackedValue;
                    var right = image[x + 1, y].PackedValue;

                    if (left > right)
                    {
                        hash |= bit;
                    }

                    bit >>= 1;
                }
            }

            return hash;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not compute perceptual hash for {Path}", imagePath);
            return null;
        }
    }

    public int HammingDistance(ulong a, ulong b) =>
        BitOperations.PopCount(a ^ b);
}
