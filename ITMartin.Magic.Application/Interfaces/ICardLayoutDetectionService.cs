using ITMartin.Magic.Application.Models;
using ITMartin.Magic.Domain;

namespace ITMartin.Magic.Application.Interfaces;

public interface ICardLayoutDetectionService
{
    Task<CardDetectionResult?> DetectAsync(
        string imagePath);
}