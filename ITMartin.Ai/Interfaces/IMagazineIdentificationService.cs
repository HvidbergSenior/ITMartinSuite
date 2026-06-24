using ITMartin.Ai.Models;

namespace ITMartin.Ai.Interfaces;

public interface IMagazineIdentificationService
{
    Task<MagazineIdentificationResult?> IdentifyAsync(
        string imagePath,
        CancellationToken cancellationToken = default);
}
