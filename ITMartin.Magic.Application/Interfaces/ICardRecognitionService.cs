using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface ICardRecognitionService
{
    Task<CardDetectionResult?> DetectAsync(
        string imagePath);
}