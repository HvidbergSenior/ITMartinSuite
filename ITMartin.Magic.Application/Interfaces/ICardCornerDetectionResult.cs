using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface ICardCornerDetectionService
{
    Task<CardCornerDetectionResult?> DetectAsync(
        string imagePath, CancellationToken cancellationToken);
}