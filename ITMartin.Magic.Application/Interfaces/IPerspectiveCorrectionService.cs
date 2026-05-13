using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface IPerspectiveCorrectionService
{
    Task<string?> CorrectAsync(
        string originalImagePath,
        CardCornerDetectionResult corners);
}