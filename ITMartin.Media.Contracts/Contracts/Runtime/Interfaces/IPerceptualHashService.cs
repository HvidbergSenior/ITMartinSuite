namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

public interface IPerceptualHashService
{
    /// <summary>
    /// Computes a 64-bit difference hash (dHash) from the image's decoded
    /// pixels, so visually-identical photos hash the same (or very close)
    /// even when their bytes differ - e.g. the same photo re-imported from
    /// a second source (iCloud recovery, a second backup) and recompressed
    /// along the way. Returns null if the file can't be decoded as an image.
    /// </summary>
    Task<ulong?> ComputeAsync(string imagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Number of differing bits between two hashes. 0 = identical; small
    /// values (roughly under 10 of 64 bits) indicate near-identical images.
    /// </summary>
    int HammingDistance(ulong a, ulong b);
}
