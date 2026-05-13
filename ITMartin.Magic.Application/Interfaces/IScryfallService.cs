using ITMartin.Magic.Application.Models;

namespace ITMartin.Magic.Application.Interfaces;

public interface IScryfallService
{
    Task<ScryfallCard?> SearchAsync(
        CardDetectionResult detection);
}