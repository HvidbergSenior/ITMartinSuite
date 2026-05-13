using ITMartin.Magic.Application.Interfaces;
using OpenCvSharp;

namespace ITMartin.Magic.Infrastructure.Services;

public class OpenCvBlurDetectionService
    : IBlurDetectionService
{
    public async Task<double> CalculateBlurScoreAsync(
        string imagePath)
    {
        return await Task.Run(() =>
        {
            // =====================================
            // LOAD
            // =====================================

            using var image =
                Cv2.ImRead(
                    imagePath,
                    ImreadModes.Grayscale);

            if (image.Empty())
            {
                return 0;
            }

            // =====================================
            // LAPLACIAN
            // =====================================

            using var laplacian =
                new Mat();

            Cv2.Laplacian(
                image,
                laplacian,
                MatType.CV_64F);

            // =====================================
            // STANDARD DEVIATION
            // =====================================

            Cv2.MeanStdDev(
                laplacian,
                out _,
                out var stddev);

            // =====================================
            // VARIANCE
            // =====================================

            var variance =
                stddev.Val0 * stddev.Val0;

            return variance;
        });
    }

    public async Task<bool> IsBlurryAsync(
        string imagePath,
        double threshold = 120)
    {
        var score =
            await CalculateBlurScoreAsync(
                imagePath);

        return score < threshold;
    }
}