using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface ICardBoundaryDetectionService
{
    Task<CardCornerDetectionResult?> DetectAsync(
        string imagePath);
}