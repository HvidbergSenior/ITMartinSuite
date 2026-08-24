using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ITMartin.Media.Infrastructure.Images;

// Free, local counterpart to the paid Claude vision blur/solid-color check -
// same idea as the free ONNX rotation tier sitting in front of the paid
// fallback (see LibraryPolishService's TryDetectOrientationViaFacesAsync).
// Runs on a small downscaled grayscale copy, same cost class as thumbnail
// generation or PerceptualHashService's own dHash - cheap enough to run on
// every image instead of being gated behind an AI-cost toggle.
public sealed class ImageQualityService : IImageQualityService
{
    // Fixed analysis grid - large enough to catch real blur/blank content,
    // small enough that decode+resize+scan stays fast regardless of the
    // source image's real resolution.
    private const int GridSize = 128;

    // Variance-of-Laplacian is the classic cheap blur metric (lower = blurrier -
    // a sharp image has more high-frequency edge content). Calibrated for this
    // 128x128 grayscale grid specifically - not the same scale as full-resolution
    // thresholds quoted elsewhere (commonly ~100 at full res). Heuristic;
    // tune from real false-positive/negative samples if it misfires in practice.
    private const double BlurVarianceThreshold = 50.0;

    // Pixel-value variance near zero means the image is (near-)one flat
    // color - a blank/solid frame, not a real photo.
    private const double SolidColorVarianceThreshold = 4.0;

    private readonly ILogger<ImageQualityService> _logger;

    public ImageQualityService(ILogger<ImageQualityService> logger)
    {
        _logger = logger;
    }

    public async Task<(bool IsBlurry, bool IsSolidColor)> AnalyzeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await Image.LoadAsync<L8>(imagePath, cancellationToken);

            image.Mutate(x => x
                .AutoOrient()
                .Resize(new ResizeOptions { Size = new Size(GridSize, GridSize), Mode = ResizeMode.Stretch }));

            var pixels = new byte[GridSize, GridSize];
            for (var y = 0; y < GridSize; y++)
                for (var x = 0; x < GridSize; x++)
                    pixels[x, y] = image[x, y].PackedValue;

            var pixelVariance = ComputeVariance(pixels);
            var isSolidColor = pixelVariance < SolidColorVarianceThreshold;

            // No point running the Laplacian pass on something already known
            // to be a blank frame.
            var isBlurry = !isSolidColor && ComputeLaplacianVariance(pixels) < BlurVarianceThreshold;

            return (isBlurry, isSolidColor);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not analyze image quality for {Path}", imagePath);
            return (false, false);
        }
    }

    private static double ComputeVariance(byte[,] pixels)
    {
        var size = pixels.GetLength(0);
        var count = size * size;

        double sum = 0;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                sum += pixels[x, y];

        var mean = sum / count;

        double sumSq = 0;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var diff = pixels[x, y] - mean;
                sumSq += diff * diff;
            }

        return sumSq / count;
    }

    // Discrete Laplacian (edge-detection) kernel applied at every interior
    // pixel; the variance of the responses is the standard cheap blur metric -
    // a sharp image has strong, varied edge responses, a blurry one has weak,
    // uniform ones.
    private static double ComputeLaplacianVariance(byte[,] pixels)
    {
        var size = pixels.GetLength(0);
        var responses = new List<double>((size - 2) * (size - 2));

        for (var y = 1; y < size - 1; y++)
        {
            for (var x = 1; x < size - 1; x++)
            {
                var value =
                    -4.0 * pixels[x, y] +
                    pixels[x - 1, y] +
                    pixels[x + 1, y] +
                    pixels[x, y - 1] +
                    pixels[x, y + 1];

                responses.Add(value);
            }
        }

        if (responses.Count == 0) return 0;

        var mean = responses.Average();
        return responses.Sum(r => (r - mean) * (r - mean)) / responses.Count;
    }
}
