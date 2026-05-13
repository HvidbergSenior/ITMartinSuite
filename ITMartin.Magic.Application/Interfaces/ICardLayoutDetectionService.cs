using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Domain;

namespace ITMartin.Magic.Application.Interfaces;

public interface ICardLayoutDetectionService
{
    CardLayoutType Detect(
        string normalizedCardPath);
}