namespace ITMartin.Magic.Application.Interfaces;

public interface IBlurDetectionService
{
    Task<double> CalculateBlurScoreAsync(
        string imagePath);

    Task<bool> IsBlurryAsync(
        string imagePath,
        double threshold = 120);
}