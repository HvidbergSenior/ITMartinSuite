using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Persistence;

public interface IAnalogDigitizeManifestStore
{
    Task SaveAsync(
        AnalogDigitizeManifest manifest,
        CancellationToken cancellationToken = default);

    Task<AnalogDigitizeManifest?> GetAsync(
        Guid packageId,
        CancellationToken cancellationToken = default);
}