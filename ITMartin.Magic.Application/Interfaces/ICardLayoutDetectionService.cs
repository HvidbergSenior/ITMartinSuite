using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface ICardLayoutDetectionService
{
    Task<CardLayoutType> DetectAsync(string stateImagePath, CancellationToken cancellationToken);
}