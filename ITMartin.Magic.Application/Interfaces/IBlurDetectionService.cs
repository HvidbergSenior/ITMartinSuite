namespace ITMartin.Magic.Application.Interfaces;

public interface IBlurDetectionService
{
    Task<double> CalculateBlurScoreAsync(
        string imagePath,
        CancellationToken cancellationToken = default);

    Task<bool> IsBlurryAsync(
        string imagePath,
        double threshold = 120,
        CancellationToken cancellationToken = default);
}