using ITMartin.Ai.Models;
using ITMartin.Magic.Application.Models;
using ITMartin.OCR.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface ICardLayoutDetectionService
{
    CardLayoutType Detect(
        string imagePath);
}